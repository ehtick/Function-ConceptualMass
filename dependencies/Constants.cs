using Elements;
using Elements.Geometry;

namespace CreateEnvelopes
{
    public class Constants
    {
        public const double FeetToMeters = 0.3048;
        public const double DEFAULT_MAX_HEIGHT = 200 * FeetToMeters;
        public const double DEFAULT_FLOOR_TO_FLOOR = 11 * FeetToMeters;
        public const double DEFAULT_BAR_WIDTH = (27 * FeetToMeters) * 2 + (5 * FeetToMeters);

        public const int DEFAULT_LEVEL_COUNT = 1;

        public static Color ENVELOPE_COLOR = new Color("#799FBB") { Alpha = 0.3 };
        public static Color EDGE_COLOR = new Color("#AAB8C1");

        public static readonly Material LEVEL_MATERIAL = new Material("Level") { Color = new Color("#F1F2F4") { Alpha = 0.5 }, SpecularFactor = 0.5, GlossinessFactor = 0 };

    }
}