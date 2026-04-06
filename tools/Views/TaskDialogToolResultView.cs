using Autodesk.Revit.UI;

namespace DeepBimMCPTools
{
    public sealed class TaskDialogToolResultView : IToolResultView
    {
        public void ShowSuccess(string title, string message)
        {
            TaskDialog.Show(title, message);
        }

        public void ShowError(string title, string message)
        {
            TaskDialog.Show(title, message);
        }
    }
}
