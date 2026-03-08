using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestSayHelloCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var parameters = new JObject { ["message"] = "Hello from Tools panel" };
                object result = DirectCommandInvoker.Invoke(commandData.Application, "say_hello", parameters);
                string text = result is JToken j ? j.ToString() : (result?.ToString() ?? "");
                if (text.Length > 500) text = text.Substring(0, 500) + "...";
                TaskDialog.Show("Test: say_hello", text);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Test: say_hello failed", ex.Message);
                return Result.Failed;
            }
        }
    }
}
