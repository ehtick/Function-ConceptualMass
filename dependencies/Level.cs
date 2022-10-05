using Newtonsoft.Json;

namespace Elements
{
    public partial class Level
    {
        [JsonIgnore]
        public int Index { get; set; }
    }
}