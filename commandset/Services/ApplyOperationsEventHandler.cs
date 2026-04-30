using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class ApplyOperationsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string Mode { get; set; } = "execute";
        public JArray Operations { get; set; } = new JArray();
        public object Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 60000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void SetParameters(string mode, JArray operations)
        {
            Mode = mode;
            Operations = operations;
            _resetEvent.Reset();
        }

        // ──────────────────────────────────────────────────────────────────────
        // IExternalEventHandler
        // ──────────────────────────────────────────────────────────────────────

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var doc = uiapp.ActiveUIDocument.Document;

                if (Mode == "preview")
                {
                    Result = ExecutePreview(doc);
                }
                else
                {
                    Result = ExecuteTransaction(doc);
                }
            }
            catch (Exception ex)
            {
                Result = new
                {
                    success = false,
                    stage = "revit_transaction",
                    message = ex.Message,
                    rolledBack = true
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "ApplyOperationsEventHandler";

        // ──────────────────────────────────────────────────────────────────────
        // Preview (validate only, no model changes)
        // ──────────────────────────────────────────────────────────────────────

        private object ExecutePreview(Document doc)
        {
            var summary = new List<string>();

            for (int i = 0; i < Operations.Count; i++)
            {
                var op = Operations[i] as JObject;
                if (op == null)
                {
                    return new
                    {
                        success = false,
                        stage = "validation",
                        failedOperationIndex = i,
                        failedOperation = (object)null,
                        message = $"Operation {i} is not a valid object."
                    };
                }

                string opName = op["op"]?.ToString();
                if (string.IsNullOrWhiteSpace(opName))
                {
                    return new
                    {
                        success = false,
                        stage = "validation",
                        failedOperationIndex = i,
                        failedOperation = op,
                        message = $"Operation {i} is missing field 'op'."
                    };
                }

                var validationError = ValidateOperation(op, opName, i);
                if (validationError != null)
                    return validationError;

                summary.Add($"Operation {i}: {opName}");
            }

            return new
            {
                success = true,
                mode = "preview",
                summary
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Execute inside TransactionGroup
        // ──────────────────────────────────────────────────────────────────────

        private object ExecuteTransaction(Document doc)
        {
            var results = new List<object>();

            using (var tg = new TransactionGroup(doc, "AI Apply Operations"))
            {
                tg.Start();

                try
                {
                    using (var tx = new Transaction(doc, "Apply Operations"))
                    {
                        tx.Start();

                        for (int i = 0; i < Operations.Count; i++)
                        {
                            var op = Operations[i] as JObject;
                            if (op == null)
                            {
                                tx.RollBack();
                                tg.RollBack();
                                return new
                                {
                                    success = false,
                                    stage = "validation",
                                    failedOperationIndex = i,
                                    failedOperation = (object)null,
                                    message = $"Operation {i} is not a valid object.",
                                    rolledBack = true
                                };
                            }

                            string opName = op["op"]?.ToString();
                            if (string.IsNullOrWhiteSpace(opName))
                            {
                                tx.RollBack();
                                tg.RollBack();
                                return new
                                {
                                    success = false,
                                    stage = "validation",
                                    failedOperationIndex = i,
                                    failedOperation = op,
                                    message = $"Operation {i}: missing field 'op'.",
                                    rolledBack = true
                                };
                            }

                            var validationError = ValidateOperation(op, opName, i);
                            if (validationError != null)
                            {
                                tx.RollBack();
                                tg.RollBack();
                                return validationError;
                            }

                            var result = ExecuteOneOperation(doc, op, opName);

                            if (!result.Success)
                            {
                                tx.RollBack();
                                tg.RollBack();
                                return new
                                {
                                    success = false,
                                    stage = "revit_transaction",
                                    failedOperationIndex = i,
                                    failedOperation = op,
                                    message = result.Message,
                                    rolledBack = true
                                };
                            }

                            results.Add(new
                            {
                                success = true,
                                message = result.Message,
                                elementId = result.ElementId
                            });
                        }

                        tx.Commit();
                    }

                    tg.Assimilate();
                }
                catch (Exception ex)
                {
                    try { tg.RollBack(); } catch { /* already rolled back */ }
                    return new
                    {
                        success = false,
                        stage = "revit_transaction",
                        message = ex.Message,
                        rolledBack = true
                    };
                }
            }

            return new
            {
                success = true,
                mode = "execute",
                results
            };
        }

        // ──────────────────────────────────────────────────────────────────────
        // Field validation (before transaction)
        // ──────────────────────────────────────────────────────────────────────

        private object ValidateOperation(JObject op, string opName, int index)
        {
            string missingField = null;

            switch (opName)
            {
                case "create_level":
                    if (op["name"] == null) missingField = "name";
                    else if (op["elevation"] == null) missingField = "elevation";
                    break;

                case "create_grid_line":
                    if (op["name"] == null) missingField = "name";
                    else if (op["start"] == null) missingField = "start";
                    else if (op["end"] == null) missingField = "end";
                    break;

                case "create_wall_by_level":
                    if (op["typeName"] == null) missingField = "typeName";
                    else if (op["levelName"] == null) missingField = "levelName";
                    else if (op["start"] == null) missingField = "start";
                    else if (op["end"] == null) missingField = "end";
                    else if (op["height"] == null) missingField = "height";
                    break;

                default:
                    return new
                    {
                        success = false,
                        stage = "validation",
                        failedOperationIndex = index,
                        failedOperation = op,
                        message = $"Unknown operation: {opName}"
                    };
            }

            if (missingField != null)
            {
                return new
                {
                    success = false,
                    stage = "validation",
                    failedOperationIndex = index,
                    failedOperation = op,
                    message = $"{opName} requires field '{missingField}'."
                };
            }

            return null;
        }

        // ──────────────────────────────────────────────────────────────────────
        // Dispatch
        // ──────────────────────────────────────────────────────────────────────

        private OperationResult ExecuteOneOperation(Document doc, JObject op, string opName)
        {
            switch (opName)
            {
                case "create_level":
                    return CreateLevel(doc, op);
                case "create_grid_line":
                    return CreateGridLine(doc, op);
                case "create_wall_by_level":
                    return CreateWallByLevel(doc, op);
                default:
                    return OperationResult.Fail($"Unknown operation: {opName}");
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // create_level
        // ──────────────────────────────────────────────────────────────────────

        private OperationResult CreateLevel(Document doc, JObject op)
        {
            string name = op["name"].ToString();
            double elevationMm = op["elevation"].ToObject<double>();

            // Check if already exists
            var existing = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => l.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (existing != null)
            {
                return OperationResult.Ok(
                    $"Level '{name}' already exists.",
#if REVIT2024_OR_GREATER
                    (int)existing.Id.Value
#else
                    existing.Id.IntegerValue
#endif
                );
            }

            double elevationFeet = MmToFeet(elevationMm);
            Level level = Level.Create(doc, elevationFeet);
            level.Name = name;

            return OperationResult.Ok(
                $"Created level: {name}",
#if REVIT2024_OR_GREATER
                (int)level.Id.Value
#else
                level.Id.IntegerValue
#endif
            );
        }

        // ──────────────────────────────────────────────────────────────────────
        // create_grid_line
        // ──────────────────────────────────────────────────────────────────────

        private OperationResult CreateGridLine(Document doc, JObject op)
        {
            string name = op["name"].ToString();
            XYZ start = ToXyzFromMm(op["start"]);
            XYZ end = ToXyzFromMm(op["end"]);

            Line line = Line.CreateBound(start, end);
            Grid grid = Grid.Create(doc, line);
            grid.Name = name;

            return OperationResult.Ok(
                $"Created grid line: {name}",
#if REVIT2024_OR_GREATER
                (int)grid.Id.Value
#else
                grid.Id.IntegerValue
#endif
            );
        }

        // ──────────────────────────────────────────────────────────────────────
        // create_wall_by_level
        // ──────────────────────────────────────────────────────────────────────

        private OperationResult CreateWallByLevel(Document doc, JObject op)
        {
            string typeName = op["typeName"].ToString();
            string levelName = op["levelName"].ToString();
            XYZ start = ToXyzFromMm(op["start"]);
            XYZ end = ToXyzFromMm(op["end"]);
            double heightFeet = MmToFeet(op["height"].ToObject<double>());

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

            return OperationResult.Ok(
                $"Created wall on level '{levelName}'.",
#if REVIT2024_OR_GREATER
                (int)wall.Id.Value
#else
                wall.Id.IntegerValue
#endif
            );
        }

        // ──────────────────────────────────────────────────────────────────────
        // Utilities
        // ──────────────────────────────────────────────────────────────────────

        private static double MmToFeet(double mm) => mm / 304.8;

        private static double FeetToMm(double feet) => feet * 304.8;

        private static XYZ ToXyzFromMm(JToken token)
        {
            double[] values = token.ToObject<double[]>();
            if (values == null || values.Length != 3)
                throw new ArgumentException("Point must be [x, y, z] in mm.");
            return new XYZ(MmToFeet(values[0]), MmToFeet(values[1]), MmToFeet(values[2]));
        }
    }
}
