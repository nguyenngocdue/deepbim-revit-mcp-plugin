using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestGetCurrentViewInfoCommand : BaseToolCommand<GetCurrentViewInfoToolViewModel>
    {
        protected override GetCurrentViewInfoToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new GetCurrentViewInfoToolViewModel(executionService);
        }
    }
}
