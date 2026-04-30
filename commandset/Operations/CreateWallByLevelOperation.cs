using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    public class CreateWallByLevelOperation : IOperationHandler
    {
        public string OpName => "create_wall_by_level";
        public string[] RequiredFields => ["typeName", "levelName", "start", "end", "height"];

        public OperationResult Execute(Document doc, JObject op)
        {
            string typeName = op["typeName"]!.ToString();
            string levelName = op["levelName"]!.ToString();
            XYZ start = OperationUtils.ToXyzFromMm(op["start"]!);
            XYZ end = OperationUtils.ToXyzFromMm(op["end"]!);
            double heightFeet = OperationUtils.MmToFeet(op["height"]!.ToObject<double>());

            var wallType = new FilteredElementCollector(doc)
                .OfClass(typeof(WallType))
                .Cast<WallType>()
                .FirstOrDefault(wt => wt.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase));
            if (wallType == null)
                return OperationResult.Fail($"WallType not found: {typeName}");

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (level == null)
                return OperationResult.Fail($"Level not found: {levelName}");

            Line line = Line.CreateBound(start, end);
            Wall wall = Wall.Create(doc, line, wallType.Id, level.Id, heightFeet, 0, false, false);
            return OperationResult.Ok($"Created wall on level '{levelName}'.", OperationUtils.GetElementId(wall));
        }
    }
}
