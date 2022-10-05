using Newtonsoft.Json;

namespace Elements
{
    public partial class Site
    {
        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        public string GenerateNewAddId()
        {
            var centroid = Perimeter.Centroid();
            // I know this is goofy ok
            return $"{(int)centroid.X + 452 * 100:X}-{(int)centroid.Y + 91 * 100:X}-{(int)centroid.Z + 13 * 100:X}";
        }
    }
}