using System;
using System.Collections.Generic;
using Elements.Annotations;
using Elements.Geometry;
using Elements.Geometry.Solids;

namespace Elements
{
    public class UnitDefinition : GeometricElement
    {
        public double Width { get; set; }
        public double Depth { get; set; }

        public Guid Balcony { get; set; }
    }
}