using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Elements
{
    public class LevelGroup : Element
    {

        public List<Level> Levels { get; set; }

        [JsonProperty("Level Group Id")]
        public string LevelGroupId { get; set; }

        [JsonIgnore]
        public double? MaxHeight => Levels.OrderBy(l => l.Elevation).LastOrDefault()?.Elevation;

        public Guid? Site { get; set; }

        public List<Level> GetLevelsUpToHeight(double height, int offset = 0)
        {
            return Levels.Where(l => l.Elevation + (l.Height ?? 0) < height + 0.01).Skip(offset).ToList();
        }

        public List<Level> GetNLevels(int count, int offset = 0)
        {
            var levelElements = new List<Level>();
            for (int i = 0; i < Levels.Count; i++)
            {
                var level = Levels[i];
                level.Index = i;
                if (level.Height != null)
                {
                    levelElements.Add(level);
                }
            }
            var levels = levelElements.Skip(offset).Take(count).ToList();
            if (levels.Count < count)
            {
                CreateEnvelopes.Logging.LogWarning($"A mass was set to have {count} Levels, but there were not enough levels defined in the level group. The last valid level is being repeated.");
            }
            while (levels.Count < count)
            {
                var lastLevel = levels.LastOrDefault() ?? levelElements.LastOrDefault();
                var levelToAdd = new Level(lastLevel.Elevation + lastLevel.Height ?? 3, 3) { Name = "Unspecified Level" };
                levels.Add(levelToAdd);
            }
            return levels;
        }
    }
}