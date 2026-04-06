using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public sealed class ExtractElementSurfacesExecutionService : IToolExecutionService
    {
        private readonly GeometrySurfaceExtractionService _geometryService;

        public ExtractElementSurfacesExecutionService(GeometrySurfaceExtractionService geometryService)
        {
            _geometryService = geometryService;
        }

        public object Execute(UIApplication uiApp, string methodName, JObject parameters)
        {
            var options = new SurfaceExtractionOptions
            {
                IncludeMeshVertices = parameters?["includeMeshVertices"]?.Value<bool>() ?? true,
                IncludeNonVisibleObjects = parameters?["includeNonVisibleObjects"]?.Value<bool>() ?? false,
                MaxVerticesPerSurface = parameters?["maxVerticesPerSurface"]?.Value<int>() ?? 24
            };

            if (options.MaxVerticesPerSurface < 0)
                options.MaxVerticesPerSurface = 0;

            return _geometryService.ExtractFromSelection(uiApp, options);
        }
    }
}
