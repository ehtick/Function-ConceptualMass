using System.Collections.Generic;
using System.Linq;
using Elements.Geometry;
using Elements.Geometry.Solids;
using CreateEnvelopes;
using Newtonsoft.Json;
using System;

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

        // Boundary is the drawn outer boundary of the envelope. If we're
        // studying different massing strategies, they will create a smaller
        // profile within this boundary.

        [JsonProperty("Boundary")]
        public Profile Boundary { get; set; }
        public Envelope(MassingOverrideAddition add)
        {
            Profile = add.Value.Boundary;
            Boundary = Profile;
            FloorToFloorHeight = add.Value.FloorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR;
            Height = FloorToFloorHeight * add.Value.Levels;
            AddId = add.Id;
            Levels = add.Value.Levels;
            Initialize();
        }

        public Envelope(Profile boundary, double maxHeight, double? floorToFloorHeight = null)
        {
            Profile = boundary;
            Boundary = boundary;
            FloorToFloorHeight = floorToFloorHeight ?? Constants.DEFAULT_FLOOR_TO_FLOOR;
            Levels = (int)Math.Floor(maxHeight / FloorToFloorHeight);
            Height = FloorToFloorHeight * Levels;
            Initialize();
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
    }
}