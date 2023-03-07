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

        [JsonProperty("Level Name")]

        public string LevelName { get; set; }

        public string LevelGroupId { get; set; }

        [JsonProperty("Level Group")]
        public string LevelGroup { get; set; }
        public Guid Envelope { get; set; }
        public List<Line> Skeleton { get; set; }

        [JsonProperty("Primary Use Category")]
        public string PrimaryUseCategory { get; set; }

        public bool Match(LevelSettingsIdentity identity)
        {
            if (AddId == identity.AddId)
            {
                return true;
            }
            else
            {
                // the level id which is the second part of the add ID can
                // mutate on clone, if it's never been edited by the user. fall
                // back to using the level name + group name to make a match.
                if (AddId.Split("-")[0] == identity.AddId.Split("-")[0])
                {
                    if (identity.LevelName == LevelName && identity.LevelGroup == LevelGroup)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        public LevelVolume Update(LevelSettingsOverride edit)
        {
            PrimaryUseCategory = edit.Value.PrimaryUseCategory ?? PrimaryUseCategory;
            return this;
        }
    }
}