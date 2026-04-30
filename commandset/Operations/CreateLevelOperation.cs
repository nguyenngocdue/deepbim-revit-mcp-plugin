using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    public class CreateLevelOperation : IOperationHandler
    {
        public string OpName => "create_level";
        public string[] RequiredFields => ["name", "elevation"];

        public OperationResult Execute(Document doc, JObject op)
        {
            string name = op["name"]!.ToString();
            double elevationMm = op["elevation"]!.ToObject<double>();

            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
                return OperationResult.Ok($"Level '{name}' already exists.", OperationUtils.GetElementId(existing));

            Level level = Level.Create(doc, OperationUtils.MmToFeet(elevationMm));
            level.Name = name;
            return OperationResult.Ok($"Created level: {name}", OperationUtils.GetElementId(level));
        }
    }
}
