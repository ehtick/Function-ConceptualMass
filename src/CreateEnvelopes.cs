using Elements;
using Elements.Geometry;
using Elements.Geometry.Solids;
using System;
using System.Collections.Generic;
using System.Linq;

namespace CreateEnvelopes
{
    public static class CreateEnvelopes
    {
        /// <summary>
        /// The CreateEnvelopes function.
        /// </summary>
        /// <param name="model">The input model.</param>
        /// <param name="input">The arguments to the execution.</param>
        /// <returns>A CreateEnvelopesOutputs instance containing computed results and the model with any new elements.</returns>
        public static CreateEnvelopesOutputs Execute(Dictionary<string, Model> inputModels, CreateEnvelopesInputs input)
        {
            ThemeColors.Initialize();
            var output = new CreateEnvelopesOutputs();
            Logging.SetLogTarget(output);
            var hasSite = inputModels.TryGetValue("Site", out var siteModel);
            var hasSiteConstraints = inputModels.TryGetValue("Site Constraints", out var siteConstraintsModel);
            var hasUnitDefinitions = inputModels.TryGetValue("Unit Definitions", out var residentialUnitsModel);
            var levelsModel = inputModels["Levels"];

            inputModels.TryGetValue("Multifamily Prompt", out var promptModel);

            // Get the width of any bar masses, based on site constraints. 
            var barWidth = ComputeBarWidth(hasSiteConstraints, siteConstraintsModel, hasUnitDefinitions, residentialUnitsModel);
            // generate default masses based on sites + site constraints
            var masses = CreateDefaultMasses(siteModel, siteConstraintsModel, levelsModel, input, barWidth, promptModel);
            // process add / edit overrides for Masses
            var addIds = new List<string>();
            var overriddenMasses = input.Overrides.Massing.CreateElements(
                input.Overrides.Additions.Massing,
                input.Overrides.Removals.Massing,
                (add) =>
                {
                    var env = new ConceptualMass(add, barWidth);
                    addIds.Add(env.AddId);
                    return env;
                },
                (elem, identity) => elem.AddId == identity.AddId,
                (elem, edit) => elem.Update(edit, barWidth)
                ).ToDictionary((env) => env.AddId);

            var orderedAddedMasses = addIds.Select((id) => overriddenMasses[id]);
            masses.AddRange(orderedAddedMasses);
            // Apply "massing strategy settings" overrides, to set skeleton
            // changes and override bar width.
            input.Overrides.MassingStrategySettings.Apply(
                masses,
                (elem, identity) => elem.AddId == identity.AddId,
                (elem, edit) => elem.ApplyMassingSettings(edit)
                );
            masses = masses.OrderBy(m => m.BottomLevel.Elevation).ToList();
            var buildings = StackMasses(masses, levelsModel, hasUnitDefinitions ? "Residential" : "Office");
            // Apply building name overrides, and update mass names to match their building
            var overriddenBuildings = input.Overrides.BuildingInfo.Apply(
                buildings,
                (bldg, identity) => bldg.MassAddIds.Any(id => identity.MassAddIds.Any(massId => massId == id)),
                (bldg, edit) => bldg.Update(edit, masses));
            output.Model.AddElements(masses);
            output.Model.AddElements(overriddenBuildings);
            output.Model.AddElements(masses.SelectMany(e => e.GetDimensions()));
            output.Model.AddElements(masses.SelectMany(e => e.GetLevelDisplay()));
            var scopes = new List<ViewScope>();
            var levelVolumes = masses.SelectMany(e => e.GetLevelVolumes(scopes)).ToList();
            levelVolumes = input.Overrides.LevelSettings.Apply(
                levelVolumes,
                (elem, identity) => elem.Match(identity),
                (elem, edit) => elem.Update(edit));
            output.Model.AddElements(levelVolumes);
            var areaTallies = ComputeLevelAreas(levelVolumes);
            output.Model.AddElements(areaTallies);
            output.Model.AddElements(scopes);
            return output;
        }

        private static List<AreaTally> ComputeLevelAreas(List<LevelVolume> levelVolumes)
        {
            var areaTallies = new Dictionary<string, AreaTally>();
            foreach (var lv in levelVolumes)
            {
                var primaryUse = lv.PrimaryUseCategory;
                if (!areaTallies.ContainsKey(primaryUse))
                {
                    var color = ProgramUseColors.GetColor(primaryUse);
                    areaTallies.Add(primaryUse, new AreaTally
                    {
                        Name = primaryUse,
                        AchievedArea = lv.Area,
                        ProgramColor = color,
                        AchievedCount = 1
                    });
                }
                else
                {
                    areaTallies[primaryUse].AchievedArea += lv.Area;
                    areaTallies[primaryUse].AchievedCount += 1;
                }
            }
            return areaTallies.Values.ToList();
        }

        private static double ComputeBarWidth(bool hasSiteConstraints, Model siteConstraintsModel, bool hasUnitDefinitions, Model residentialUnitsModel)
        {
            var barWidth = Constants.DEFAULT_BAR_WIDTH;
            if (hasUnitDefinitions)
            {
                double? balconyOffset = null;
                if (hasSiteConstraints)
                {
                    var setbacks = siteConstraintsModel.AllElementsOfType<Setback>();
                    var setbacksWithBalconyRule = setbacks.Where(s => s.BalconyProtrusionDepth != null);
                    if (setbacksWithBalconyRule.Count() > 0)
                    {
                        balconyOffset = setbacksWithBalconyRule.Max(s => s.BalconyProtrusionDepth.Value);
                    }
                }
                var unitDefs = residentialUnitsModel.AllElementsOfType<UnitDefinition>();
                // choose the largest depth. Include balconies if balconyOffset != null. 
                var greatestDepth = unitDefs.Max(u =>
                {
                    if (balconyOffset != null)
                    {
                        residentialUnitsModel.Elements.TryGetValue(u.Balcony, out var balcony);
                        if (balcony != null)
                        {
                            var bbox = new BBox3(balcony);
                            var bDepth = bbox.Max.Y - bbox.Min.Y;
                            return u.Depth + bDepth - balconyOffset.Value;
                        }
                    }
                    return u.Depth;
                });
                barWidth = greatestDepth * 2 + Units.FeetToMeters(5);
            }

            return barWidth;
        }

        public static Site CreateDefaultSite()
        {
            var rect = Polygon.Rectangle(100, 100);
            return new Site(rect, rect.Area());
        }

        public static List<ConceptualMass> CreateDefaultMasses(Model siteModel, Model siteConstraintsModel, Model levelsModel, CreateEnvelopesInputs input, double barWidth, Model promptModel)
        {
            var removedAddIds = new HashSet<string>(input.Overrides.Removals.Massing.Select((r) => r.Identity.AddId));
            var overridesByAddId = input.Overrides.Massing.ToDictionary((o) => o.Identity.AddId);
            var list = new List<ConceptualMass>();
            var sites = siteModel?.AllElementsOfType<Site>() ?? new List<Site>();

            var allSetbacks = siteConstraintsModel?.AllElementsOfType<Setback>() ?? new List<Setback>();
            var allSiteConstraints = siteConstraintsModel?.AllElementsOfType<SiteConstraintInfo>() ?? new List<SiteConstraintInfo>();
            var allLevelGroups = levelsModel.AllElementsOfType<LevelGroup>();

            // Injected settings from an optional AI prompt dependency. 
            var conceptualMassSettings = promptModel?.AllElementsOfType<ConceptualMassSettingsElement>().Select(s => s.Settings).OrderBy(s => s.BottomLevelIndex ?? 0).ToList() ?? new List<ConceptualMassSettings>();

            foreach (var site in sites)
            {
                var setbacksForSite = allSetbacks.Where(s => s.Site == site.Id).ToList();
                var constraintForSite = allSiteConstraints.FirstOrDefault(s => s.Site == site.Id);
                var levelGroupForSite = allLevelGroups.FirstOrDefault(l => l.Site == site.Id) ?? allLevelGroups.First();
                var elevationChanges = new HashSet<double>
                {
                    0.0
                };
                foreach (var setback in setbacksForSite)
                {
                    elevationChanges.Add(setback.StartingHeight);
                }
                var maxHeight = levelGroupForSite.MaxHeight ?? constraintForSite?.MaxHeight ?? Constants.DEFAULT_MAX_HEIGHT;
                foreach (var settings in conceptualMassSettings)
                {
                    if (settings.BottomLevelIndex.HasValue)
                    {
                        var bottomLevel = levelGroupForSite.Levels[settings.BottomLevelIndex.Value];
                        elevationChanges.Add(bottomLevel.Elevation);
                    }
                    if (settings.TopLevelIndex.HasValue)
                    {
                        var topLevel = levelGroupForSite.Levels[settings.TopLevelIndex.Value];
                        elevationChanges.Add(topLevel.Elevation);
                        maxHeight = topLevel.Elevation;
                    }
                }
                elevationChanges.Add(maxHeight);
                var elevationChangesSorted = elevationChanges.OrderBy(e => e).ToList();
                // If we create a volume that's less than the intended height
                // because of floor-to-floor, pass on the remainder to the next
                // envelope.
                var leftoverHeight = 0.0;
                Level lastLevelUsed = null;
                for (int i = 0; i < elevationChangesSorted.Count - 1; i++)
                {
                    var addId = $"{site.AddId ?? $"{site.GenerateNewAddId()}"}-{i}";

                    var settingsForLevel = conceptualMassSettings.ElementAtOrDefault(i);

                    var curr = elevationChangesSorted[i];
                    var next = elevationChangesSorted[i + 1];
                    var envHeight = next - curr + leftoverHeight;
                    // Override Removals
                    if (removedAddIds.Contains(addId))
                    {
                        leftoverHeight = envHeight;
                        continue;
                    }
                    Profile baseProfile = null;

                    List<Level> levels = levelGroupForSite.GetLevelsUpToHeight(next, lastLevelUsed);
                    var strategy = settingsForLevel?.GetMassingStrategy() ?? "Full";
                    string primaryUseCategory = settingsForLevel?.PrimaryUseCategory ?? null;
                    // Override Edits
                    if (overridesByAddId.TryGetValue(addId, out var overrideVal))
                    {
                        if (overrideVal.Value.TopLevel != null || overrideVal.Value.BottomLevel != null)
                        {
                            var topLevel = overrideVal.Value.TopLevel == null ? levels.Last() : levelGroupForSite.FindBestMatch(overrideVal.Value.TopLevel.Id, overrideVal.Value.TopLevel.Elevation);
                            var bottomLevel = overrideVal.Value.BottomLevel == null ? levels.First() : levelGroupForSite.FindBestMatch(overrideVal.Value.BottomLevel.Id, overrideVal.Value.BottomLevel.Elevation);
                            levels = levelGroupForSite.GetLevelsBetween(bottomLevel, topLevel);
                        }
                        baseProfile = overrideVal.Value.Boundary;
                        if (overrideVal.Value.MassingStrategy != null)
                        {
                            strategy = Hypar.Model.Utilities.GetStringValueFromEnum(overrideVal.Value.MassingStrategy);
                        }
                        // if the user has chosen a strategy other than "Full", and not set a primary use category, let's assume residential, since bars typically are.
                        primaryUseCategory = overrideVal.Value.PrimaryUseCategory ?? (overrideVal.Value.MassingStrategy != null ? "Residential" : primaryUseCategory);
                    }
                    lastLevelUsed = levels.Last();

                    // Default profile behavior
                    var setbacksAtHeight = setbacksForSite.Where(s => curr >= s.StartingHeight).ToList();
                    baseProfile ??= CreateProfileFromSetbacks(site.Perimeter, setbacksAtHeight);
                    var env = new ConceptualMass(baseProfile, levels, levelGroupForSite)
                    {
                        AddId = addId,
                        PrimaryUseCategory = primaryUseCategory,
                        Settings = settingsForLevel,
                    };
                    if (overrideVal != null)
                    {
                        Identity.AddOverrideIdentity(env, overrideVal);
                    }
                    env.ApplyMassingStrategy(strategy, barWidth);
                    leftoverHeight = envHeight - env.Height;
                    list.Add(env);
                }
            }

            return list;
        }

        private static Profile CreateProfileFromSetbacks(Polygon perimeter, List<Setback> setbacksAtHeight)
        {
            // The strategy here is effectively a 2d boolean, but we compute it
            // a different way in order to get clean trims "for free". We
            // *could* create a bunch of rectangles and polygon/profile boolean,
            // but that gets very messy at concave corners.

            // The idea is:
            // - create an ordered list of lines (perimeter edges)
            // - for each setback, replace the line with an appropriately offset line
            // - take the resulting list of lines and intersect each one with its two neighbors
            // - construct a polygon from the resulting line/line intersections
            var lines = new List<Line>();
            var remainingSetbacks = new List<Setback>(setbacksAtHeight);
            foreach (var polygonSegment in perimeter.Segments())
            {
                // There might be multiple setbacks using the same site edge. Use the one with the greatest distance.
                var matchingSetbacks = remainingSetbacks.Where(s => s.Baseline.Start.DistanceTo(polygonSegment.Start) < 0.01 && s.Baseline.End.DistanceTo(polygonSegment.End) < 0.01).ToList();
                var matchingSetback = matchingSetbacks.OrderBy(s => s.Distance).LastOrDefault();
                foreach (var ms in matchingSetbacks)
                {
                    remainingSetbacks.Remove(ms);
                }
                if (matchingSetback != null)
                {
                    lines.Add(matchingSetback.Baseline.Offset(matchingSetback.Distance, true));
                }
                else
                {
                    lines.Add(polygonSegment);
                }
            }

            var lineIntersections = new List<Vector3>();
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                var nextLine = lines[(i + 1) % lines.Count];
                if (line.Intersects(nextLine, out var intersection, true))
                {
                    lineIntersections.Add(intersection);
                }
                else
                {
                    // the case of parallel lines, maybe?
                    lineIntersections.Add(line.End);
                }
            }
            Profile profile = new Polygon(lineIntersections);

            // If there were setbacks that didn't coincide exactly with the
            // perimeter, we have to revert to a boolean strategy.
            foreach (var rs in remainingSetbacks)
            {
                var a = rs.Baseline.Offset(rs.Distance, false);
                var b = rs.Baseline.Offset(rs.Distance, true);
                var rectangleFromSetback = new Polygon(a.Start, a.End, b.End, b.Start);
                try
                {
                    var difference = Profile.Difference(new[] { profile }, new[] { new Profile(rectangleFromSetback) });
                    profile = difference.OrderBy(p => p.Area()).LastOrDefault() ?? profile;
                }
                catch
                {
                    Console.WriteLine("Setback boolean failed.");
                }
            }

            return profile;
        }

        public static List<Building> StackMasses(List<ConceptualMass> envelopes, Model levelsModel, string defaultUseCategory)
        {
            var buildings = new List<Building>();
            var levelGroups = levelsModel.AllElementsOfType<LevelGroup>();
            // TODO — permit picking the best level group based on site proximity, etc.
            var levelGroupForNewEnvelopes = levelGroups.FirstOrDefault();
            if (levelGroupForNewEnvelopes == null)
            {
                return buildings;
            }
            var seenMasses = new List<ConceptualMass>();
            foreach (var e in envelopes)
            {
                // if a user added an envelope but set neither centerline nor profile, just make it a dumb box.
                if (e.Profile == null)
                {
                    e.Profile = Polygon.Rectangle(30, 30);
                    e.Boundary = e.Profile;
                }
                var greatestHeightOfSeenMasses = 0.0;
                // walk seenMasses backwards, so that we prefer propagating
                // settings from the last envelope that was added to the stack.
                for (int i = seenMasses.Count - 1; i >= 0; i--)
                {
                    var se = seenMasses[i];
                    // If the current mass intersects the seen mass, stack on top of it, and propagate settings upwards.
                    if (se.Profile.Perimeter.Intersects(e.Profile.Perimeter))
                    {
                        // keep track of the greatest height we've seen. 
                        var heightOfMass = se.Elevation + se.Height;
                        if (heightOfMass > greatestHeightOfSeenMasses)
                        {
                            greatestHeightOfSeenMasses = heightOfMass;
                        }
                        // If the seen mass has a skeleton and we do not, propagate that skeleton to this mass.
                        if (se.Skeleton != null && e.Skeleton == null)
                        {
                            e.Skeleton = se.Skeleton;
                            e.ApplyMassingStrategy("Custom", se.BarWidth);
                        }
                        // if the seen mass has a use category and we do not, propagate that use category to this mass.
                        if (se.PrimaryUseCategory != null && e.PrimaryUseCategory == null)
                        {
                            e.PrimaryUseCategory = se.PrimaryUseCategory;
                        }
                        // if the seen mass has a building assignment and we do not, propagate that building assignment to this mass.
                        if (se.Building != null && e.Building == null)
                        {
                            e.Building = se.Building;
                            buildings.FirstOrDefault(b => b.Id == se.Building).MassAddIds.Add(e.AddId);
                        }
                    }
                }
                // if we have no levels assigned yet, assign them automatically.
                if (e.LevelElements.Count == 0)
                {
                    // top level should be either the supplied top level, or the
                    // top level of the level group. 
                    var topLevel = e.TopLevel?.Id == null ? levelGroupForNewEnvelopes.Levels.Last() : levelGroupForNewEnvelopes.FindBestMatch(e.TopLevel.Id, e.TopLevel.Elevation);
                    // bottom level should be either the supplied bottom level,
                    // or the first level that's larger than the greatest height
                    // of already-seen masses (so we stack on top). (or the
                    // lowest level of the level group, if none found.)
                    var bottomLevel = e.BottomLevel?.Id == null ?
                        levelGroupForNewEnvelopes.Levels.FirstOrDefault(l => l.Elevation > (greatestHeightOfSeenMasses - 0.01)) ?? levelGroupForNewEnvelopes.Levels.First() :
                        levelGroupForNewEnvelopes.FindBestMatch(e.BottomLevel.Id, e.BottomLevel.Elevation);
                    var levelsForEnvelope = levelGroupForNewEnvelopes.GetLevelsBetween(bottomLevel, topLevel);
                    e.SetLevelInfo(levelsForEnvelope, levelGroupForNewEnvelopes);
                }
                // if the envelope has no building assigned, that means it
                // didn't get one from a mass underneath it: assign it a new
                // building.
                if (e.Building == null)
                {
                    var building = new Building
                    {
                        Name = $"Building {buildings.Count + 1}"
                    };
                    building.MassAddIds.Add(e.AddId);
                    buildings.Add(building);
                    e.Building = building.Id;
                }
                if (e.PrimaryUseCategory == null && e.Skeleton == null && defaultUseCategory == "Residential" && e.MassingStrategy == "Full")
                {
                    e.ApplyMassingStrategy("L", e.BarWidth);
                }
                e.PrimaryUseCategory ??= defaultUseCategory;
                e.Transform = new Transform(0, 0, e.BottomLevel.Elevation);
                seenMasses.Add(e);
            }
            return buildings;
        }
    }
}