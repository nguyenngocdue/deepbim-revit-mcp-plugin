using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestGetCurrentViewInfoCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                object result = DirectCommandInvoker.Invoke(commandData.Application, "get_current_view_info");
                string text = result is JToken j ? j.ToString() : (result?.ToString() ?? "");
                if (text.Length > 800) text = text.Substring(0, 800) + "...";
                TaskDialog.Show("Test: get_current_view_info", text);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Test: get_current_view_info failed", ex.Message);
                return Result.Failed;
            }
        }
    }
}
