using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;
using System.IO;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestExportSheetsCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                string tempPath = Path.Combine(Path.GetTempPath(), "DeepBimMCP_SheetsExport_Test.xlsx");
                var parameters = new JObject
                {
                    ["outputPath"] = tempPath,
                    ["propertyNames"] = new JArray { "Sheet Number", "Sheet Name" }
                };
                object result = DirectCommandInvoker.Invoke(commandData.Application, "export_sheets_to_excel", parameters);
                string text = result is JToken j ? j.ToString() : (result?.ToString() ?? "");
                TaskDialog.Show("Test: export_sheets_to_excel", text + "\n\nFile: " + tempPath);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Test: export_sheets_to_excel failed", ex.Message);
                return Result.Failed;
            }
        }
    }
}
