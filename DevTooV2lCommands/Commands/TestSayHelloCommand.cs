
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DevTooV2lCommands.Models;
using DevTooV2lCommands.ViewModes;

namespace DevTooV2lCommands
{
    [Transaction(TransactionMode.Manual)]
    public class TestSayHelloCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            // 1. Create a model to hold the data for the view
            var model = new SayHelloModel
            {
                Message = "Hello, Revit! from DevTooV2lCommands"
            };
            // 2. Create the view and set its data context to the model
            var viewModel = new SayHelloViewModel(model);

            // 3.Run busines logic
            viewModel.Run();

            // 4. Display the result ( View - TaskDialog  acts as the view)
            if (viewModel.IsSuccess)
            {
                TaskDialog.Show("Success", viewModel.Result);
                return Result.Succeeded;
            }
            TaskDialog.Show("Error", viewModel.Result);
            return Result.Failed;
        }
    }
}
