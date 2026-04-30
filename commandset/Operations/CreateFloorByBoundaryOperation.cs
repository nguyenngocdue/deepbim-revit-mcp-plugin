using Autodesk.Revit.DB;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;

namespace RevitMCPCommandSet.Operations
{
    /// <summary>
    /// Creates a floor slab from a boundary polygon on a given level.
    /// Input:
    ///   levelName:     string   — target level name
    ///   floorTypeName: string   — floor type name
    ///   boundary:      number[][] — list of [x, y] or [x, y, z] points in mm forming a closed polygon (min 3 points)
    /// </summary>
    public class CreateFloorByBoundaryOperation : IOperationHandler
    {
        public string OpName => "create_floor_by_boundary";
        public string[] RequiredFields => ["levelName", "floorTypeName", "boundary"];

        public OperationResult Execute(Document doc, JObject op)
        {
            string levelName = op["levelName"]!.ToString();
            string floorTypeName = op["floorTypeName"]!.ToString();

            var level = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(levelName, StringComparison.OrdinalIgnoreCase));
            if (level == null)
                return OperationResult.Fail($"Level not found: {levelName}");

            var floorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FloorType))
                .Cast<FloorType>()
                .FirstOrDefault(ft => ft.Name.Equals(floorTypeName, StringComparison.OrdinalIgnoreCase));
            if (floorType == null)
                return OperationResult.Fail($"FloorType not found: {floorTypeName}");

            double[][] rawPoints = op["boundary"]!.ToObject<double[][]>()
                ?? throw new ArgumentException("boundary must be an array of [x,y] or [x,y,z] points.");
            if (rawPoints.Length < 3)
                return OperationResult.Fail("boundary must contain at least 3 points.");

            double z = level.Elevation;
            var pts = rawPoints.Select(p => new XYZ(
                OperationUtils.MmToFeet(p[0]),
                OperationUtils.MmToFeet(p[1]),
                z)).ToList();

#if REVIT2022_OR_GREATER
            var loop = new CurveLoop();
            for (int i = 0; i < pts.Count; i++)
                loop.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));

            Floor floor = Floor.Create(doc, new List<CurveLoop> { loop }, floorType.Id, level.Id);
#else
            var curves = new CurveArray();
            for (int i = 0; i < pts.Count; i++)
                curves.Append(Line.CreateBound(pts[i], pts[(i + 1) % pts.Count]));

            Floor floor = doc.Create.NewFloor(curves, floorType, level, false);
#endif
            return OperationResult.Ok(
                $"Created floor on level '{levelName}'.",
                OperationUtils.GetElementId(floor));
        }
    }
}
