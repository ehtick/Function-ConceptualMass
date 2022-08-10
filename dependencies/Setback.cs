using System.Collections.Generic;
using Elements.Geometry;
using Elements.Geometry.Solids;
using Newtonsoft.Json;

namespace Elements
{
    public partial class Setback : GeometricElement
    {

        public double EndingHeight { get; set; }

        [JsonProperty("Add Id")]
        public string AddId { get; set; }

        private Extrude Extrude
        {
            get
            {
                var offsetLine = this.Baseline.Offset(this.Distance, false);
                Validators.Validator.DisableValidationOnConstruction = true;
                var profile = new Polygon(Baseline.Start, Baseline.End, offsetLine.End, offsetLine.Start).TransformedPolygon(new Transform(0, 0, StartingHeight));
                var extrude = new Extrude(profile, EndingHeight - StartingHeight, Vector3.ZAxis, false);
                Validators.Validator.DisableValidationOnConstruction = false;
                return extrude;
            }
        }

    }
}