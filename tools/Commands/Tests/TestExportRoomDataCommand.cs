using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestExportRoomDataCommand : BaseToolCommand<ExportRoomDataToolViewModel>
    {
        protected override ExportRoomDataToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new ExportRoomDataToolViewModel(executionService);
        }
    }
}
