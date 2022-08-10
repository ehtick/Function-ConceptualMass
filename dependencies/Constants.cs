using Elements;
using Elements.Geometry;

namespace CreateEnvelopes
{
    public class Constants
    {
        public static double DEFAULT_MAX_HEIGHT = Units.FeetToMeters(200);
        public static double DEFAULT_FLOOR_TO_FLOOR = Units.FeetToMeters(11);

        public static Color ENVELOPE_COLOR = new Color("#799FBB") { Alpha = 0.3 };
    }
}