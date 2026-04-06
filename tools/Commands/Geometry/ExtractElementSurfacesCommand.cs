using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class ExtractElementSurfacesCommand : BaseToolCommand<ExtractElementSurfacesToolViewModel>
    {
        protected override IToolExecutionService CreateExecutionService(ICommandGateway gateway)
        {
            return new ExtractElementSurfacesExecutionService(new GeometrySurfaceExtractionService());
        }

        protected override ExtractElementSurfacesToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new ExtractElementSurfacesToolViewModel(executionService);
        }
    }
}
