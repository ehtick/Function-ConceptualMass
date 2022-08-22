using System.Collections.Generic;
using Elements.Geometry;
using glTFLoader.Schema;

namespace Elements
{
    public class DisplayLines : ModelLines
    {
        public DisplayLines(IEnumerable<Line> lines, double lineWidth = 1)
        {
            this.Lines = new List<Line>(lines);
            this.SetSelectable(false);
            this.Material = new Material("Level Lines")
            {
                Color = CreateEnvelopes.Constants.EDGE_COLOR,
                EdgeDisplaySettings = new EdgeDisplaySettings
                {
                    WidthMode = EdgeDisplayWidthMode.ScreenUnits,
                    LineWidth = lineWidth
                }
            };
        }
        // public override bool TryToGraphicsBuffers(out List<GraphicsBuffers> graphicsBuffers, out string id, out MeshPrimitive.ModeEnum? mode)
        // {
        //     mode = MeshPrimitive.ModeEnum.LINES;
        //     id = "unselectable_DisplayLines";
        //     var points = new List<Vector3>();
        //     foreach (var line in Lines)
        //     {
        //         points.Add(line.Start);
        //         points.Add(line.End);
        //     }
        //     graphicsBuffers = new List<GraphicsBuffers> { points.ToGraphicsBuffers() };
        //     return true;
        // }

    }
}