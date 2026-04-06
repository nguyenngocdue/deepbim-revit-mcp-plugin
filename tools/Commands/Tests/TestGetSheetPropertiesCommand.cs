using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestGetSheetPropertiesCommand : BaseToolCommand<GetSheetPropertiesToolViewModel>
    {
        protected override GetSheetPropertiesToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new GetSheetPropertiesToolViewModel(executionService);
        }
    }
}
