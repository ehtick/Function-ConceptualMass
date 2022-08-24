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
            var output = new CreateEnvelopesOutputs();
            var hasSite = inputModels.TryGetValue("Site", out var siteModel);
            var hasSiteConstraints = inputModels.TryGetValue("Site Constraints", out var siteConstraintsModel);
            var hasUnitDefinitions = inputModels.TryGetValue("Unit Definitions", out var residentialUnitsModel);
            var barWidth = Constants.DEFAULT_BAR_WIDTH;
            if (hasUnitDefinitions)
            {
                var unitDefs = residentialUnitsModel.AllElementsOfType<UnitDefinition>();
                // choose the largest depth:
                var greatestDepth = unitDefs.Max(u => u.Depth);
                barWidth = greatestDepth * 2 + Units.FeetToMeters(5);
            }
            var masses = CreateDefaultMasses(siteModel, siteConstraintsModel, input, barWidth);
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
                (elem, identity) =>
                {
                    return elem.AddId == identity.AddId;
                },
                (elem, edit) =>
                {
                    elem.Update(edit, barWidth);

                    return elem;
                }).ToDictionary((env) => env.AddId);
            var orderedAddedMasses = addIds.Select((id) => overriddenMasses[id]);
            masses.AddRange(orderedAddedMasses);
            input.Overrides.MassingStrategySettings.Apply(
                masses,
                (elem, identity) => elem.AddId == identity.AddId,
                (elem, edit) =>
                {
                    elem.ApplyMassingSettings(edit);
                    return elem;
                });
            StackMasses(masses);
            output.Model.AddElements(masses);
            output.Model.AddElements(masses.SelectMany(e => e.GetDimensions()));
            output.Model.AddElements(masses.SelectMany(e => e.GetLevelDisplay()));
            return output;
        }

        public static Site CreateDefaultSite()
        {
            var rect = Polygon.Rectangle(100, 100);
            return new Site(rect, rect.Area());
        }

        public static List<ConceptualMass> CreateDefaultMasses(Model siteModel, Model siteConstraintsModel, CreateEnvelopesInputs input, double barWidth)
        {
            var removedAddIds = new HashSet<string>(input.Overrides.Removals.Massing.Select((r) => r.Identity.AddId));
            var overridesByAddId = input.Overrides.Massing.ToDictionary((o) => o.Identity.AddId);

            var list = new List<ConceptualMass>();
            var sites = siteModel?.AllElementsOfType<Site>() ?? new List<Site>() { CreateDefaultSite() };
            var allSetbacks = siteConstraintsModel?.AllElementsOfType<Setback>() ?? new List<Setback>();
            var allSiteConstraints = siteConstraintsModel?.AllElementsOfType<SiteConstraintInfo>() ?? new List<SiteConstraintInfo>();
            foreach (var site in sites)
            {
                var setbacksForSite = allSetbacks.Where(s => s.Site == site.Id).ToList();
                var constraintForSite = allSiteConstraints.FirstOrDefault(s => s.Site == site.Id);
                var elevationChanges = new HashSet<double>();
                elevationChanges.Add(0.0);
                foreach (var setback in setbacksForSite)
                {
                    elevationChanges.Add(setback.StartingHeight);
                }
                var maxHeight = constraintForSite?.MaxHeight ?? Constants.DEFAULT_MAX_HEIGHT;
                elevationChanges.Add(maxHeight);
                var elevationChangesSorted = elevationChanges.OrderBy(e => e).ToList();
                // If we create a volume that's less than the intended height
                // because of floor-to-floor, pass on the remainder to the next
                // envelope.
                var leftoverHeight = 0.0;
                for (int i = 0; i < elevationChangesSorted.Count - 1; i++)
                {
                    var addId = $"{site.AddId ?? site.Perimeter.Centroid().ToString()}-{i}";

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
                    double? floorToFloor = null;
                    int? levels = null;
                    var strategy = "Full";
                    var primaryUseCategory = "Office";
                    // Override Edits
                    if (overridesByAddId.TryGetValue(addId, out var overrideVal))
                    {
                        floorToFloor = overrideVal.Value.FloorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR;
                        levels = overrideVal.Value.Levels ?? (int)Math.Floor(envHeight / floorToFloor.Value);
                        baseProfile = overrideVal.Value.Boundary;
                        if (overrideVal.Value.MassingStrategy != null)
                        {
                            strategy = Hypar.Model.Utilities.GetStringValueFromEnum(overrideVal.Value.MassingStrategy);
                        }
                        // if the user has chosen a strategy other than "Full", and not set a primary use category, let's assume residential, since bars typically are.
                        primaryUseCategory = overrideVal.Value.PrimaryUseCategory ?? (overrideVal.Value.MassingStrategy != null ? "Residential" : "Office");
                    }
                    // Default profile behavior
                    var setbacksAtHeight = setbacksForSite.Where(s => curr >= s.StartingHeight).ToList();
                    baseProfile ??= CreateProfileFromSetbacks(site.Perimeter, setbacksAtHeight);
                    var env = new ConceptualMass(baseProfile, envHeight, floorToFloor, levels)
                    {
                        AddId = addId,
                        PrimaryUseCategory = primaryUseCategory,
                    };
                    env.ApplyMassingStrategy(strategy, barWidth);
                    // More Override Edits
                    if (overrideVal != null)
                    {
                        if (overrideVal.Value.FloorToFloorHeights != null)
                        {
                            env.SetFloorToFloorHeights(overrideVal.Value.FloorToFloorHeights.ToList());
                        }
                    }
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

        public static void StackMasses(List<ConceptualMass> envelopes)
        {
            var seenMasses = new List<ConceptualMass>();
            foreach (var e in envelopes)
            {
                foreach (var se in seenMasses)
                {
                    if (se.Profile.Perimeter.Intersects(e.Profile.Perimeter))
                    {
                        e.Transform.Move((0, 0, se.Height));
                        e.Elevation = e.Transform.Origin.Z;
                    }
                }
                seenMasses.Add(e);
            }
        }
    }
}