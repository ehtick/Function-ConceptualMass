using System;

namespace Elements
{
    public class SiteConstraintInfo : Element
    {
        public double? MinFAR { get; set; }
        public double? MaxFAR { get; set; }
        public double? MinHeight { get; set; }
        public double? MaxHeight { get; set; }
        public double? MinLotCoverage { get; set; }
        public double? MaxLotCoverage { get; set; }

        public string SiteAddId { get; set; }

        public Guid Site { get; set; }
    }
}