using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    public class CreateGridLineOperation : IOperationHandler
    {
        public string OpName => "create_grid_line";
        public string[] RequiredFields => ["name", "start", "end"];

        public OperationResult Execute(Document doc, JObject op)
        {
            string name = op["name"]!.ToString();
            XYZ start = OperationUtils.ToXyzFromMm(op["start"]!);
            XYZ end = OperationUtils.ToXyzFromMm(op["end"]!);

            Line line = Line.CreateBound(start, end);
            Grid grid = Grid.Create(doc, line);
            grid.Name = name;
            return OperationResult.Ok($"Created grid line: {name}", OperationUtils.GetElementId(grid));
        }
    }
}
