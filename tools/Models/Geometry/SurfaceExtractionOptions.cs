namespace DeepBimMCPTools
{
    public sealed class SurfaceExtractionOptions
    {
        public bool IncludeMeshVertices { get; set; } = true;
        public bool IncludeNonVisibleObjects { get; set; } = false;
        public int MaxVerticesPerSurface { get; set; } = 24;
    }
}
