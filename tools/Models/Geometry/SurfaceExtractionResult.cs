using System.Collections.Generic;

namespace DeepBimMCPTools
{
    public sealed class SurfaceExtractionResult
    {
        public int ElementId { get; set; }
        public string ElementUniqueId { get; set; } = string.Empty;
        public string ElementName { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int SurfaceCount { get; set; }
        public double TotalAreaSquareFeet { get; set; }
        public double TotalAreaSquareMeters { get; set; }
        public List<SurfaceModel> Surfaces { get; set; } = new List<SurfaceModel>();
    }
}
