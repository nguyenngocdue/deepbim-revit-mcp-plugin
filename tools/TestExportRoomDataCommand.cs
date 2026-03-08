using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using System;

namespace DeepBimMCPTools
{
    [Transaction(TransactionMode.Manual)]
    public class TestExportRoomDataCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var parameters = new JObject
                {
                    ["includeUnplacedRooms"] = false,
                    ["includeNotEnclosedRooms"] = false
                };
                object result = DirectCommandInvoker.Invoke(commandData.Application, "export_room_data", parameters);
                string text = result is JToken j ? j.ToString() : (result?.ToString() ?? "");
                if (text.Length > 800) text = text.Substring(0, 800) + "...";
                TaskDialog.Show("Test: export_room_data", text);
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Test: export_room_data failed", ex.Message);
                return Result.Failed;
            }
        }
    }
}
