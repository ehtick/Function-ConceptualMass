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
    public partial class ConceptualMass
    {
        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        public int Levels { get; set; }

        [JsonProperty("Floor to Floor Height")]
        public double FloorToFloorHeight { get; set; }

        [JsonProperty("Massing Strategy")]
        public string MassingStrategy { get; set; } = "Full";

        [JsonProperty("Skeleton")]
        public List<Line> Skeleton { get; set; }

        [JsonProperty("Bar Width")]
        public double BarWidth { get; set; }

        // Boundary is the drawn outer boundary of the envelope. If we're
        // studying different massing strategies, they will create a smaller
        // profile within this boundary.

        [JsonProperty("Boundary", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Profile Boundary { get; set; }
        public ConceptualMass(MassingOverrideAddition add, double barWidth)
        {
            Profile = add.Value.Boundary;
            Boundary = Profile;
            SetLevelInfo(add.Value.Levels, add.Value.FloorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR);
            AddId = add.Id;
            Levels = add.Value.Levels;
            if (add.Value.Mode == MassingOverrideAdditionValueMode.Centerline)
            {
                Skeleton = add.Value.Centerline.ToList();
                ApplyMassingStrategy("Custom", barWidth);
            }
            Initialize();
        }

        public ConceptualMass(Profile boundary, double maxHeight, double? floorToFloorHeight = null, int? levels = null)
        {
            Profile = boundary;
            Boundary = boundary;
            FloorToFloorHeight = floorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR;
            Levels = levels ?? (int)Math.Floor(maxHeight / FloorToFloorHeight);
            SetLevelInfo(Levels, FloorToFloorHeight);
            Initialize();
        }

        public void SetLevelInfo(int levels, double floorToFloorHeight = Constants.DEFAULT_FLOOR_TO_FLOOR)
        {
            FloorToFloorHeight = floorToFloorHeight;
            Levels = Math.Max(1, levels);
            FloorToFloorHeights = new List<double>();
            for (int i = 0; i < levels; i++)
            {
                FloorToFloorHeights.Add(floorToFloorHeight);
            }
            Height = FloorToFloorHeight * Levels;
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
            for (int i = 0; i < FloorToFloorHeights.Count; i++)
            {
                double f2f = this.FloorToFloorHeights[i];
                var nextCorner = corner + (0, 0, f2f);
                var alignedDim = new AlignedDimension(nextCorner, corner, plane);
                alignedDim.AdditionalProperties["LinkedOverrideProperty"] = new Dictionary<string, object> {
                    { "ElementId", this.Id },
                    { "OverrideName", "Massing" },
                    { "Property", "Floor To Floor Heights" },
                    { "Index", i },
                    { "VisibleOnSelection", true }
                };
                dims.Add(alignedDim);
                corner = nextCorner;
            }
            return dims;
        }

        public List<DisplayLines> GetLevelDisplay()
        {
            var lines = new List<Line>();
            var transform = new Transform(this.Transform);
            var segments = Profile.Segments();
            lines.AddRange(segments.Select(s => s.TransformedLine(transform)));
            for (int i = 0; i < FloorToFloorHeights.Count; i++)
            {
                var f2f = FloorToFloorHeights[i];
                transform.Move(0, 0, f2f);
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
            return new List<DisplayLines> { new DisplayLines(lines, 1), new DisplayLines(edges, 2) };
        }
        public void SetFloorToFloorHeights(List<double> heights)
        {
            if (heights == null || heights.Count == 0)
            {
                return;
            }
            // use the most common value for the floor to floor height
            var modeOfHeights = heights.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key;
            FloorToFloorHeight = modeOfHeights;
            FloorToFloorHeights = heights;
            Levels = FloorToFloorHeights.Count;
            Height = FloorToFloorHeights.Sum();
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

        public void ApplyMassingSettings(MassingStrategySettingsOverride edit)
        {
            if (MassingStrategy == "Full")
            {
                return;
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
            var length = 0.0;
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
                if (bestPl == null || polyline.Length() > length)
                {
                    bestPl = polyline;
                    length = polyline.Length();
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