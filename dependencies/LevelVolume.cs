using System;
using System.Collections.Generic;
using CreateEnvelopes;
using Elements.Geometry;
using Newtonsoft.Json;

namespace Elements
{
    public partial class LevelVolume
    {
        [JsonProperty("Add Id")]
        public string AddId { get; set; }
        public Guid Envelope { get; set; }

        [JsonProperty("Plan View")]

        public ViewScope PlanView { get; set; }

        public List<Line> Skeleton { get; set; }

        [JsonProperty("Primary Use Category")]
        public string PrimaryUseCategory { get; set; }

        public Guid Level { get; set; }

        public LevelVolume Update(LevelSettingsOverride edit)
        {
            PrimaryUseCategory = edit.Value.PrimaryUseCategory ?? PrimaryUseCategory;
            return this;
        }
    }
}