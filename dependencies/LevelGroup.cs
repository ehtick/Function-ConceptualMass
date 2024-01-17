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

        public List<Level> GetLevelsUpToHeight(double height, Level aboveLevel = null)
        {
            var offset = Levels.IndexOf(aboveLevel);
            return Levels.Where(l => l.Elevation < height + 0.01).Skip(offset).ToList();
        }

        public List<Level> GetLevelsBetween(Level bottomLevel, Level topLevel)
        {
            if (Levels.Count < 2)
            {
                CreateEnvelopes.Logging.LogWarning("There are not enough levels. Add more levels to the level group.");
                return new List<Level>();
            }
            var bottomLevelIndex = Levels.IndexOf(bottomLevel);
            var topLevelIndex = Levels.IndexOf(topLevel);
            if (topLevel.Elevation <= bottomLevel.Elevation)
            {
                CreateEnvelopes.Logging.LogWarning("The top level is at or below the bottom level. Automatically fixing levels.");
                topLevelIndex = bottomLevelIndex + 1;
                if (topLevelIndex >= Levels.Count)
                {
                    topLevelIndex = Levels.Count - 1;
                    bottomLevelIndex = topLevelIndex - 1;
                }
            }
            var levels = new List<Level>();
            for (int i = bottomLevelIndex; i <= topLevelIndex; i++)
            {
                levels.Add(Levels[i]);
            }
            return levels;
        }

        public Level FindBestMatch(string id, double? elevation)
        {
            var idMatch = Levels.FirstOrDefault(l => l.Id.ToString() == id);
            if (idMatch != null)
            {
                return idMatch;
            }
            return Levels.OrderBy(l => Math.Abs(l.Elevation - (elevation ?? 0))).FirstOrDefault();
        }
    }
}