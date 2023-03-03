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
                ConceptualMassFromModules.Logging.LogWarning($"A mass was set to have {count} Levels, but there were not enough levels defined in the level group. The last valid level is being repeated.");
            }
            while (levels.Count < count)
            {
                var lastLevel = levels.LastOrDefault() ?? levelElements.LastOrDefault();
                var levelToAdd = new Level(lastLevel.Elevation + lastLevel.Height ?? 3, 3) { Name = "Unspecified Level" };
                levels.Add(levelToAdd);
            }
            return levels;
        }

        public List<Level> GetLevelsBetween(Level bottomLevel, Level topLevel)
        {
            if (Levels.Count < 2)
            {
                ConceptualMassFromModules.Logging.LogWarning("There are not enough levels. Add more levels to the level group.");
                return new List<Level>();
            }
            var bottomLevelIndex = Levels.IndexOf(bottomLevel);
            var topLevelIndex = Levels.IndexOf(topLevel);
            if (topLevel.Elevation <= bottomLevel.Elevation)
            {
                ConceptualMassFromModules.Logging.LogWarning("The top level is at or below the bottom level. Automatically fixing levels.");
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