using System.Collections.Generic;

namespace DeepBimMCPTools
{
    public sealed class SurfaceModel
    {
        public int Index { get; set; }
        public string SurfaceType { get; set; } = string.Empty;
        public bool IsPlanar { get; set; }
        public double AreaSquareFeet { get; set; }
        public double AreaSquareMeters { get; set; }
        public int EdgeLoopCount { get; set; }
        public int TriangleCount { get; set; }
        public int MaterialElementId { get; set; }
        public string StableReference { get; set; } = string.Empty;
        public SurfacePointModel? Normal { get; set; }
        public List<SurfacePointModel> SampleVertices { get; set; } = new List<SurfacePointModel>();
    }
}
