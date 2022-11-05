using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;
using CreateEnvelopes;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using Elements.Annotations;

namespace Elements
{

    public class LevelReference
    {
        public string Id { get; set; }
        public double Elevation { get; set; }

        public LevelReference()
        {

        }


        public LevelReference(MassingOverrideAdditionValueTopLevel topLevel)
        {
            Id = topLevel?.Id;
            Elevation = topLevel?.Elevation ?? 0.0;
        }

        public LevelReference(MassingOverrideAdditionValueBottomLevel bottomLevel)
        {
            Id = bottomLevel?.Id;
            Elevation = bottomLevel?.Elevation ?? 0.0;
        }

        public LevelReference(Level level)
        {
            Id = level?.Id.ToString();
            Elevation = level?.Elevation ?? 0.0;
        }

        internal void Update(MassingValueTopLevel topLevel)
        {
            Id = topLevel?.Id ?? Id;
            Elevation = topLevel?.Elevation ?? Elevation;
        }

        internal void Update(MassingValueBottomLevel bottomLevel)
        {
            Id = bottomLevel?.Id ?? Id;
            Elevation = bottomLevel?.Elevation ?? Elevation;
        }
    }
    public partial class ConceptualMass
    {
        [JsonProperty("Add Id")]
        public string AddId { get; set; }
        [JsonProperty("Top Level")]
        public LevelReference TopLevel { get; set; }

        [JsonProperty("Bottom Level")]
        public LevelReference BottomLevel { get; set; }

        [JsonProperty("Massing Strategy")]
        public string MassingStrategy { get; set; } = "Full";

        // [JsonProperty("Skeleton")]
        // public List<Line> Skeleton { get; set; }

        [JsonProperty("Bar Width")]
        public double BarWidth { get; set; }

        // [JsonProperty("Primary Use Category")]
        // public string PrimaryUseCategory { get; set; }

        // Boundary is the drawn outer boundary of the envelope. If we're
        // studying different massing strategies, they will create a smaller
        // profile within this boundary.

        [JsonProperty("Boundary", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Profile Boundary { get; set; }

        [JsonIgnore]
        public List<Level> LevelElements { get; set; } = new List<Level>();

        [JsonIgnore]
        private Guid LevelGroupId { get; set; }

        private static Plane XY = new Plane((0, 0), (0, 0, 1));

        // public List<Guid> LevelIds => 

        // public Guid? Building { get; set; }
        public ConceptualMass(MassingOverrideAddition add, double barWidth)
        {
            Profile = add.Value.Boundary?.Project(XY);
            Boundary = Profile;


            AddId = add.Id;
            TopLevel = new LevelReference(add.Value.TopLevel);
            BottomLevel = new LevelReference(add.Value.BottomLevel);

            if (add.Value.Mode == MassingOverrideAdditionValueMode.Centerline)
            {
                Skeleton = add.Value.Centerline.ToList();
                ApplyMassingStrategy("Custom", barWidth);
            }
            // we only set up the LCS at "add" time, so that it can be used for
            // downstream relative identity. We don't want it to change if the
            // user edits the mass.
            ComputeLCS();
            PrimaryUseCategory = add.Value.PrimaryUseCategory;
            Identity.AddOverrideIdentity(this, add);
            Initialize();
        }

        private void ComputeLCS()
        {
            var lcsLocation = Profile?.Perimeter?.Centroid() ?? Skeleton?.Select(sk => sk.PointAt(0.5)).Aggregate((a, b) => a + b) / (Skeleton?.Count) ?? (0, 0);
            var longestEdge = (Profile?.Perimeter?.Segments().ToList() ?? Skeleton ?? new List<Line>()).OrderByDescending(s => s.Length()).FirstOrDefault() ?? new Line((0, 0), (1, 0));
            LocalCoordinateSystem = new Transform(lcsLocation, longestEdge.Direction(), Vector3.ZAxis);
        }

        public ConceptualMass(Profile boundary, List<Level> levels, LevelGroup levelGroup)
        {
            Profile = boundary?.Project(XY);
            Boundary = boundary?.Project(XY);
            ComputeLCS();
            SetLevelInfo(levels, levelGroup);
            Initialize();
        }

        public ConceptualMass Update(MassingOverride edit, double barWidth)
        {
            Profile = edit.Value.Boundary?.Project(XY) ?? Profile;
            Boundary = edit.Value.Boundary?.Project(XY) ?? Boundary;
            PrimaryUseCategory = edit.Value.PrimaryUseCategory ?? PrimaryUseCategory;

            TopLevel.Update(edit.Value.TopLevel);
            BottomLevel.Update(edit.Value.BottomLevel);

            if (edit.Value.MassingStrategy != null)
            {
                ApplyMassingStrategy(Hypar.Model.Utilities.GetStringValueFromEnum(edit.Value.MassingStrategy), barWidth);
            }
            else if (MassingStrategy == "Custom")
            {
                // Reapply to trim with a newly modified profile.
                ApplyMassingStrategy("Custom", barWidth);
            }
            Identity.AddOverrideIdentity(this, edit);
            return this;
        }

        public void SetLevelInfo(List<Level> levels, LevelGroup levelGroup)
        {
            TopLevel = new LevelReference(levels.Last());
            BottomLevel = new LevelReference(levels.First());
            LevelElements = levels;
            LevelIds = LevelElements.Select(e => e.Id).ToList();
            LevelGroupId = levelGroup.Id;
            Height = levels.SkipLast(1).Sum(l => l.Height ?? 0);
        }

        public List<Annotation> GetDimensions()
        {
            var dims = new List<Annotation>();
            // This method should only be called after "stacking" has happened.
            var elev = Transform.Origin.Z;
            // pick any consistent corner point to use for the origin.
            var corner = Profile.Perimeter.Vertices.OrderBy(v => v.DistanceTo((-10000, -10000, -10000))).First();
            var plane = new Plane(corner, Profile.Perimeter.Segments().First().Direction().Negate());
            plane.Origin += plane.Normal * 1;
            corner.Z = elev;
            foreach (var level in LevelElements.SkipLast(1))
            {
                if (level.Height == null)
                {
                    continue;
                }
                var f2f = level.Height;
                var nextCorner = corner + (0, 0, f2f.Value);
                var alignedDim = new AlignedDimension(nextCorner, corner, plane);
                alignedDim.AdditionalProperties["LinkedProperty"] = new Dictionary<string, object> {
                    { "ElementId", this.Id },
                    { "Dependency", "Levels" },
                    { "OverrideName", "Level Groups" },
                    { "PropertyName", "Level Heights" },
                    { "OverriddenElementId", LevelGroupId },
                    { "Index", level.Index },
                    { "VisibleOnlyOnSelection", true }
                };
                dims.Add(alignedDim);
                corner = nextCorner;
            }
            return dims;
        }

        public List<ModelLines> GetLevelDisplay()
        {
            var lines = new List<Line>();
            var transform = new Transform(this.Transform);
            var segments = Profile.Segments();
            lines.AddRange(segments.Select(s => s.TransformedLine(transform)));
            foreach (var lvl in LevelElements.SkipLast(1))
            {
                if (lvl.Height == null)
                {
                    continue;
                }
                var f2f = lvl.Height;
                transform.Move(0, 0, f2f.Value);
                foreach (var s in segments)
                {
                    lines.Add(s.TransformedLine(transform));
                }
            }
            var edges = Extrude.Solid.Edges.SelectMany(e =>
            {
                var a = e.Value.Left.Vertex.Point;
                var b = e.Value.Right.Vertex.Point;
                if (a.DistanceTo(b) < 0.1)
                {
                    return new Line[] { };
                }
                return new[] { new Line(e.Value.Left.Vertex.Point, e.Value.Right.Vertex.Point).TransformedLine(Transform) };
            });
            var ml1 = new ModelLines(lines.ToList())
            {
                Material = new Material("Level Lines")
                {
                    Color = CreateEnvelopes.Constants.EDGE_COLOR,
                    EdgeDisplaySettings = new EdgeDisplaySettings
                    {
                        WidthMode = EdgeDisplayWidthMode.ScreenUnits,
                        LineWidth = 1
                    }
                }
            };
            ml1.SetSelectable(false);
            var ml2 = new ModelLines(edges.ToList())
            {
                Material = new Material("Level Edges")
                {
                    Color = CreateEnvelopes.Constants.EDGE_COLOR,
                    EdgeDisplaySettings = new EdgeDisplaySettings
                    {
                        WidthMode = EdgeDisplayWidthMode.ScreenUnits,
                        LineWidth = 2
                    }
                }
            };
            return new List<ModelLines> { ml1, ml2 };
        }

        public List<LevelVolume> GetLevelVolumes(List<ViewScope> scopes)
        {
            var list = new List<LevelVolume>();
            foreach (var lvl in LevelElements.SkipLast(1))
            {
                if (lvl.Height == null)
                {
                    continue;
                }
                var profileInset = this.Profile.Offset(-0.05).First();
                var representation = new Extrude(profileInset, lvl.Height.Value - 0.05, Vector3.ZAxis, false);

                var levelVolume = new LevelVolume
                {
                    Profile = this.Profile,
                    Height = lvl.Height.Value,
                    Area = this.Profile.Area(),
                    Name = lvl.Name,
                    Representation = representation,
                    Transform = new Transform(0, 0, lvl.Elevation),
                    BuildingName = this.Name,
                    Skeleton = this.Skeleton?.ToList(),
                    PrimaryUseCategory = this.PrimaryUseCategory,
                    Material = Constants.LEVEL_MATERIAL,
                    Level = lvl.Id,
                    Mass = this.Id,
                    Envelope = this.Id,
                    AddId = $"{this.AddId}-{lvl.Id}"
                };
                var scopeName = levelVolume.Name;
                if (!String.IsNullOrEmpty(levelVolume.BuildingName))
                {
                    scopeName = $"{levelVolume.BuildingName}: {scopeName}";
                }
                var bbox = new BBox3(levelVolume);
                // drop the box by a meter to avoid ceilings / beams, etc.
                // drop the bottom to encompass floors below
                bbox = new BBox3(bbox.Min + (0, 0, -0.3), bbox.Max + (0, 0, -1));
                var existingScope = scopes.FirstOrDefault((scope) => { return scope.Name == scopeName; });
                if (existingScope == null)
                {
                    var scope = new ViewScope(
                       bbox,
                        new Camera(default, CameraNamedPosition.Top, CameraProjection.Orthographic),
                        true,
                        name: scopeName);
                    levelVolume.PlanView = scope;
                    scopes.Add(scope);
                }
                else
                {
                    existingScope.BoundingBox = new BBox3(new[] { bbox.Min, bbox.Max, existingScope.BoundingBox.Max, existingScope.BoundingBox.Min });
                }
                list.Add(levelVolume);
            }
            return list;
        }

        public void Initialize()
        {
            Material = new Material("Conceptual Mass")
            {
                Color = Constants.ENVELOPE_COLOR,
            };
        }

        private Extrude _extrude;

        private Extrude Extrude
        {
            get
            {
                if (_extrude == null)
                {
                    _extrude = new Extrude(Profile, Height, Vector3.ZAxis, false);
                }
                return _extrude;
            }
        }
        public override void UpdateRepresentations()
        {
            this.Representation = Extrude;
        }

        public void ApplyMassingStrategy(string strategy, double barWidth)
        {
            this.MassingStrategy = strategy;
            this.BarWidth = barWidth;
            switch (this.MassingStrategy)
            {
                case "Full":
                    return;
                case "Donut":
                    Donut(Boundary, barWidth);
                    return;
                case "L":
                    L(Boundary, barWidth);
                    return;
                case "U":
                    U(Boundary, barWidth);
                    return;
                case "H":
                    H(Boundary, barWidth);
                    return;
                case "Bar":
                    Bar(Boundary, barWidth);
                    return;
                case "Custom":
                    Custom(Boundary, barWidth, Skeleton);
                    return;
            }
        }

        public ConceptualMass ApplyMassingSettings(MassingStrategySettingsOverride edit)
        {
            if (MassingStrategy == "Full" && edit.Value.Skeleton == null)
            {
                return this;
            }
            var val = edit.Value;
            if (val.BarWidth != null)
            {
                BarWidth = val.BarWidth.Value;
            }
            if (edit.Value.Skeleton != null && edit.Value.Skeleton.Count > 0)
            {
                Skeleton = edit.Value.Skeleton.ToList();
                MassingStrategy = "Custom";
            }
            ApplyMassingStrategy(MassingStrategy, BarWidth);
            this.AddOverrideIdentity(edit);
            return this;
        }

        private void Donut(Profile p, double dist)
        {
            var offset = p.Offset(-dist);
            var skeleton = p.Offset(-dist / 2);
            var voidCrv = offset.OrderBy(o => o.Area()).LastOrDefault();
            if (voidCrv == null)
            {
                Profile = p;
                return;
            }
            var diff = Profile.Difference(new[] { p }, new[] { voidCrv }).OrderBy(o => o.Area()).LastOrDefault();
            if (diff == null)
            {
                diff = p;
                MassingStrategy = "Full";
            }

            Profile = diff;
            Skeleton = skeleton.SelectMany(s => s.Segments()).ToList();
        }

        private void NLongestContiguousSegments(Profile p, double dist, int n)
        {
            var perim = p.Perimeter;
            var vertices = perim.Vertices;
            Polyline bestPl = null;
            var bestScore = Double.MinValue;
            if (vertices.Count < n + 1)
            {
                Profile = p;
                return;
            }
            for (int i = 0; i < vertices.Count; i++)
            {
                var verts = new List<Vector3>();
                for (int j = 0; j <= n; j++)
                {
                    verts.Add(vertices[(i + j) % vertices.Count]);
                }
                var currVertex = i;
                var nextVertex = (i + 1) % vertices.Count;
                var nextNextVertex = (i + 2) % vertices.Count;
                var polyline = new Polyline(verts);
                var score = polyline.Length();
                // prefer solutions that keep the ends further apart
                if (polyline.Start.DistanceTo(polyline.End) < dist * 4)
                {
                    var penalty = (dist * 4) - polyline.Start.DistanceTo(polyline.End);
                    score -= penalty * 3;
                }
                if (bestPl == null || score > bestScore)
                {
                    bestPl = polyline;
                    bestScore = score;
                }
            }
            var offset = bestPl.OffsetOpen(dist / 2);
            var innerOffset = bestPl.OffsetOpen(-dist / 2);
            var thickened = bestPl.Offset(dist, EndType.Square);
            var intersected = Profile.Intersection(new[] { p }, thickened.Select(t => new Profile(t)));
            var intersection = intersected.OrderBy(o => o.Area()).LastOrDefault();
            if (intersection == null)
            {
                Profile = p;
                MassingStrategy = "Full";
                return;
            }
            Profile = intersection;
            Skeleton = innerOffset.Segments().ToList();
        }

        private void L(Profile p, double dist)
        {
            NLongestContiguousSegments(p, dist, 2);
        }

        private void U(Profile p, double dist)
        {
            NLongestContiguousSegments(p, dist, 3);
        }

        private void H(Profile p, double dist)
        {
            var segments = p.Perimeter.CollinearPointsRemoved().Segments();
            var bestLength = 0.0;
            List<Line> winningSkeleton = null;
            foreach (var s in segments)
            {
                var perp = s.Direction().Cross(Vector3.ZAxis);
                var midPt = (s.PointAt(0.5) + p.Perimeter.Centroid()) * 0.5;
                var centerBar = new Line(midPt, midPt + perp * -0.1);
                var extended = centerBar.ExtendTo(segments);
                var oppSegment = segments.FirstOrDefault(s => extended.End.DistanceTo(s) < 0.01);
                var offset1 = oppSegment.Offset(dist / 2, true);
                var offset2 = s.Offset(dist / 2, true);
                centerBar.Intersects(offset1, out var ptA, true);
                centerBar.Intersects(offset2, out var ptB, true);
                centerBar = new Line(ptA, ptB);
                var skeleton = new List<Line> { centerBar, offset1, offset2 };
                var length = skeleton.Sum(l => l.Length());
                if (length > bestLength)
                {
                    winningSkeleton = skeleton;
                    bestLength = length;
                }
            }
            var skeletonOffsets = winningSkeleton.SelectMany(l => l.ToPolyline(1).Offset(dist / 2, EndType.Square)).Select(p => new Profile(p));
            var intersected = Profile.Intersection(skeletonOffsets, new[] { p });
            var intersection = intersected.OrderBy(o => o.Area()).LastOrDefault();
            if (intersection != null)
            {
                Profile = intersection;
                Skeleton = winningSkeleton;
            }
            else
            {
                Profile = p;
                MassingStrategy = "Full";
            }
        }

        private void Bar(Profile p, double dist)
        {
            var centroid = p.Perimeter.Centroid();
            var dirs = p.Perimeter.Segments().Select(s => s.Direction()).ToList();
            var bestLength = 0.0;
            Line bestBar = null;
            foreach (var dir in dirs)
            {
                var l = new Line(centroid, centroid + dir * 0.1);
                var extended = l.ExtendTo(p.Perimeter);
                var length = extended.Length();
                if (length > bestLength)
                {
                    bestBar = extended;
                    bestLength = length;
                }
            }
            var offset = bestBar.ToPolyline().Offset(dist / 2, EndType.Square).Select(p => new Profile(p));
            var intersected = Profile.Intersection(offset, new[] { p });
            var intersection = intersected.OrderBy(o => o.Area()).LastOrDefault();
            if (intersection != null)
            {
                Profile = intersection;
                Skeleton = new List<Line> { bestBar };
            }
            else
            {
                Profile = p;
                MassingStrategy = "Full";
            }
        }
        private void Custom(Profile p, double dist, IEnumerable<Line> skeleton)
        {
            var skeletonJoined = TryJoinPolylines(skeleton.Select(s => s.ToPolyline(1)));
            var skeletonOffsets = skeletonJoined.SelectMany(l => l.Offset(dist / 2, EndType.Square)).Select(p => new Profile(p));
            var intersected = p == null ? Profile.UnionAll(skeletonOffsets) : Profile.Intersection(skeletonOffsets, new[] { p });
            var intersection = intersected.OrderBy(o => o.Area()).LastOrDefault();
            if (intersection == null)
            {
                Profile = p;
                MassingStrategy = "Full";
                return;
            }
            Profile = intersection;
        }

        private List<Polyline> TryJoinPolylines(IEnumerable<Polyline> skeleton, List<Polyline> currentPolylines = null, int depth = 0)
        {
            if (depth > 50)
            {
                return currentPolylines;
            }
            if (currentPolylines == null)
            {
                currentPolylines = new List<Polyline>();
            }
            foreach (var pl in skeleton)
            {
                if (currentPolylines.Count == 0)
                {
                    currentPolylines.Add(pl);
                }
                else
                {
                    var startToStart = currentPolylines.FirstOrDefault(p => p.Start.DistanceTo(pl.Start) < 0.01);
                    if (startToStart != null)
                    {
                        // E--pl--S S--p--E
                        currentPolylines.Remove(startToStart);
                        var verts = new List<Vector3>(pl.Vertices.Skip(1).Reverse());
                        verts.AddRange(startToStart.Vertices);
                        var newPl = new Polyline(verts);
                        TryJoinPolylines(new[] { newPl }, currentPolylines, depth + 1);
                        continue;
                    }
                    var startToEnd = currentPolylines.FirstOrDefault(p => p.End.DistanceTo(pl.Start) < 0.01);
                    if (startToEnd != null)
                    {
                        // S--p--E S--pl--E
                        currentPolylines.Remove(startToEnd);
                        var verts = new List<Vector3>(startToEnd.Vertices);
                        verts.AddRange(pl.Vertices.Skip(1));
                        var newPl = new Polyline(verts);
                        TryJoinPolylines(new[] { newPl }, currentPolylines, depth + 1);
                        continue;
                    }
                    var endToStart = currentPolylines.FirstOrDefault(p => p.Start.DistanceTo(pl.End) < 0.01);
                    if (endToStart != null)
                    {
                        // S--pl--E S--p--E
                        currentPolylines.Remove(endToStart);
                        var verts = new List<Vector3>(pl.Vertices.SkipLast(1));
                        verts.AddRange(endToStart.Vertices);
                        var newPl = new Polyline(verts);
                        TryJoinPolylines(new[] { newPl }, currentPolylines, depth + 1);
                        continue;
                    }
                    var endToEnd = currentPolylines.FirstOrDefault(p => p.End.DistanceTo(pl.End) < 0.01);
                    if (endToEnd != null)
                    {
                        // S--p--E E--pl--S
                        currentPolylines.Remove(endToEnd);
                        var verts = new List<Vector3>(endToEnd.Vertices);
                        verts.AddRange(pl.Vertices.SkipLast(1).Reverse());
                        var newPl = new Polyline(verts);
                        TryJoinPolylines(new[] { newPl }, currentPolylines, depth + 1);
                        continue;
                    }
                    currentPolylines.Add(pl);
                }
            }
            return currentPolylines;
        }

        [Conditional("DEBUG")]
        public static void JsonDump(object o, string name)
        {
            Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = true;
            if (o is Profile p)
            {
                o = new Profile(p.Perimeter, p.Voids);
            }
            var s = JsonConvert.SerializeObject(o);
            Serialization.JSON.JsonInheritanceConverter.ElementwiseSerialization = false;
            System.IO.File.WriteAllText($"../../../../{name}.json", s);
        }
    }
}