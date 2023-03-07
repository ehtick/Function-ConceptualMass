using Newtonsoft.Json;

namespace Elements
{
    public partial class Level
    {
        [JsonIgnore]
        public int Index { get; set; }

        public string LevelGroupId { get; set; }

        [JsonProperty("Level Group")]
        public string LevelGroup { get; set; }
    }
}