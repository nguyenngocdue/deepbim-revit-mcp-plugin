using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestSayHelloCommand : BaseToolCommand<SayHelloToolViewModel>
    {
        protected override SayHelloToolViewModel CreateViewModel(IToolExecutionService executionService)
        {
            return new SayHelloToolViewModel(executionService);
        }
    }
}
