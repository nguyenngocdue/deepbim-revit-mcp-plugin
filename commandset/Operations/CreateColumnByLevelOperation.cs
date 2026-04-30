using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    /// <summary>
    /// Places a structural column family instance at (x, y) on a given level.
    /// Input:
    ///   familyTypeName: string  — FamilySymbol name or "FamilyName - TypeName"
    ///   levelName:      string  — level to attach the column base to
    ///   x:              number  — X position in mm
    ///   y:              number  — Y position in mm
    /// </summary>
    public class CreateColumnByLevelOperation : IOperationHandler
    {
        public string OpName => "create_column_by_level";
        public string[] RequiredFields => ["familyTypeName", "levelName", "x", "y"];

        public OperationResult Execute(Document doc, JObject op)
        {
            string familyTypeName = op["familyTypeName"]!.ToString();
            string levelName = op["levelName"]!.ToString();
            double xMm = op["x"]!.ToObject<double>();
            double yMm = op["y"]!.ToObject<double>();

            var familySymbol = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs =>
                    fs.Name.Equals(familyTypeName, StringComparison.OrdinalIgnoreCase) ||
                    $"{fs.FamilyName} - {fs.Name}".Equals(familyTypeName, StringComparison.OrdinalIgnoreCase));

            if (familySymbol == null)
                return OperationResult.Fail($"Family type not found: {familyTypeName}");

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (level == null)
                return OperationResult.Fail($"Level not found: {levelName}");

            if (!familySymbol.IsActive)
                familySymbol.Activate();

            XYZ location = new XYZ(OperationUtils.MmToFeet(xMm), OperationUtils.MmToFeet(yMm), 0);
            FamilyInstance column = doc.Create.NewFamilyInstance(
                location, familySymbol, level, StructuralType.Column);

            return OperationResult.Ok(
                $"Created column '{familyTypeName}' at ({xMm},{yMm}) on '{levelName}'.",
                OperationUtils.GetElementId(column));
        }
    }
}
