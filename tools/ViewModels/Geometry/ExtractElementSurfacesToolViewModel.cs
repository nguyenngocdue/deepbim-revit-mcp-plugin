using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public sealed class ExtractElementSurfacesToolViewModel : ToolViewModelBase
    {
        public ExtractElementSurfacesToolViewModel(IToolExecutionService executionService) : base(executionService)
        {
        }

        public override string MethodName => "extract_element_surfaces";
        public override string DialogTitle => "Extract Element Surfaces";
        public override int MaxResultLength => 4000;

        public bool IncludeMeshVertices { get; set; } = true;
        public bool IncludeNonVisibleObjects { get; set; }
        public int MaxVerticesPerSurface { get; set; } = 24;

        protected override JObject BuildParameters()
        {
            return new JObject
            {
                ["includeMeshVertices"] = IncludeMeshVertices,
                ["includeNonVisibleObjects"] = IncludeNonVisibleObjects,
                ["maxVerticesPerSurface"] = MaxVerticesPerSurface
            };
        }
    }
}
