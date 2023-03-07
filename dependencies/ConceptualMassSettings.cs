
using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Elements
{
    public class ConceptualMassSettings
    {
        public string MassingStrategy { get; set; }
        public int? TopLevelIndex { get; set; }
        public int? BottomLevelIndex { get; set; }
        public string PrimaryUseCategory { get; set; }

        public Dictionary<int, string> LevelUseCategories { get; set; }

        public string Name { get; set; }

        public string GetMassingStrategy()
        {
            if (MassingStrategy != null)
            {
                return MassingStrategy;
            }
            if (PrimaryUseCategory == "Residential")
            {
                return "Bar";
            };
            if (PrimaryUseCategory == "Hotel")
            {
                return "Bar";
            }
            return null;
        }

    }

    public class ConceptualMassSettingsElement : Element
    {
        public ConceptualMassSettings Settings { get; set; }
    }
}