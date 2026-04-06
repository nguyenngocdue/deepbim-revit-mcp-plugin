using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestExportSheetsCommand : BaseToolCommand<ExportSheetsToolViewModel>
    {
        protected override ExportSheetsToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new ExportSheetsToolViewModel(executionService);
        }
    }
}
