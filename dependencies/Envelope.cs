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
    public partial class Envelope
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

        [JsonProperty("Floor To Floor Heights")]
        public List<double> FloorToFloorHeights { get; set; }

        // Boundary is the drawn outer boundary of the envelope. If we're
        // studying different massing strategies, they will create a smaller
        // profile within this boundary.

        [JsonProperty("Boundary", Required = Newtonsoft.Json.Required.DisallowNull, NullValueHandling = Newtonsoft.Json.NullValueHandling.Ignore)]
        public Profile Boundary { get; set; }
        public Envelope(MassingOverrideAddition add)
        {
            Profile = add.Value.Boundary;
            Boundary = Profile;
            SetLevelInfo(add.Value.Levels, add.Value.FloorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR);
            AddId = add.Id;
            Levels = add.Value.Levels;
            Initialize();
        }

        public Envelope(Profile boundary, double maxHeight, double? floorToFloorHeight = null, int? levels = null)
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
            Levels = levels;
            FloorToFloorHeights = new List<double>();
            for (int i = 0; i < levels; i++)
            {
                FloorToFloorHeights.Add(floorToFloorHeight);
            }
            Height = FloorToFloorHeight * levels;
        }

        public void SetFloorToFloorHeights(List<double> heights)
        {
            // use the most common value for the floor to floor height
            var modeOfHeights = heights.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key;
            FloorToFloorHeight = modeOfHeights;
            FloorToFloorHeights = heights;
            Levels = FloorToFloorHeights.Count;
            Height = FloorToFloorHeights.Sum();

            Color c = new Color();
            c = null;
        }

        public void Initialize()
        {
            Material = new Material("Envelope")
            {
                Color = Constants.ENVELOPE_COLOR,
            };
        }

        public override void UpdateRepresentations()
        {
            this.Representation = new Extrude(Profile, Height, Vector3.ZAxis, false);
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
            var skeletonOffsets = skeleton.SelectMany(l => l.ToPolyline(1).Offset(dist / 2, EndType.Square)).Select(p => new Profile(p));
            var intersected = Profile.Intersection(skeletonOffsets, new[] { p });
            var intersection = intersected.OrderBy(o => o.Area()).LastOrDefault();
            if (intersection == null)
            {
                Profile = p;
                MassingStrategy = "Full";
                return;
            }
            Profile = intersection;
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