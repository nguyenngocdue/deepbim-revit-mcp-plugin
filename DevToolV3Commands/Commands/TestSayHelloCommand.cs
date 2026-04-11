using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace DevToolV3Commands
{
    [Transaction(TransactionMode.Manual)]
    public class TestSayHelloCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            TaskDialog.Show("Hello", "Hello, Revit!");
            return Result.Succeeded;
        }
    }
}
