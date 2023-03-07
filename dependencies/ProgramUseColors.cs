using System;
using System.Collections.Generic;
using Elements.Geometry;

namespace Elements
{
    public static class ThemeColors
    {
        public static void Initialize(int indexOffset = 0)
        {
            currentColorIndex = 0 + indexOffset;
            random = new Random(11);
        }
        private static Random random = new Random(11);
        public static Color Blue1 = new Color("#3286C3");
        public static Color Green1 = new Color("#7ECD9F");
        public static Color Pink1 = new Color("#F9B9BF");
        public static Color Blue2 = new Color("#799FBB");
        public static Color Green2 = new Color("#CBE790");
        public static Color Red1 = new Color("#D2454B");
        public static Color Orange1 = new Color("#F2994A");
        public static Color Yellow1 = new Color("#F2C94C");
        public static Color Purple1 = new Color("#8E79BB");
        public static Color Cyan1 = new Color("#8FC7D4");
        public static Color Pink2 = new Color("#F15C6C");
        public static Color Purple2 = new Color("#C7A5BF");

        public static Color Gray1 = new Color("#747F90");

        public static Color Gray2 = new Color("#DDDFE4");

        private static int currentColorIndex = 0;

        private static readonly List<Color> AllColors = new List<Color>
        {
            Blue1,
            Green1,
            Pink1,
            Blue2,
            Green2,
            Red1,
            Orange1,
            Yellow1,
            Purple1,
            Cyan1,
            Pink2,
            Purple2,
            Gray1,
            Gray2
        };

        public static Color Next()
        {
            if (currentColorIndex >= AllColors.Count)
            {
                return random.NextColor();
            }
            else
            {
                return AllColors[currentColorIndex++];
            }
        }
    }

    public static class ProgramUseColors
    {
        public static Dictionary<string, Color> Colors = new Dictionary<string, Color>() {
            {"Residential", ThemeColors.Green1},
            {"Office", ThemeColors.Blue1},
            {"Retail", ThemeColors.Red1},
            {"Hotel", ThemeColors.Purple1},
            {"Parking", ThemeColors.Gray1},
            {"Industrial", ThemeColors.Yellow1},
            {"Warehouse", ThemeColors.Gray2},
            {"Healthcare", ThemeColors.Pink2},
        };

        public static Color GetColor(string use)
        {
            if (Colors.ContainsKey(use))
            {
                return Colors[use];
            }
            else
            {
                var color = ThemeColors.Next();
                Colors.Add(use, color);
                return color;
            }
        }
    }
}