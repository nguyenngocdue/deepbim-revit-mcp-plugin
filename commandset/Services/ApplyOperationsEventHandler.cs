using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Models;
using RevitMCPCommandSet.Operations;
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

        // ── Preview ───────────────────────────────────────────────────────────

        private object ExecutePreview(Document doc)
        {
            var summary = new List<string>();
            for (int i = 0; i < Operations.Count; i++)
            {
                var op = Operations[i] as JObject;
                string? err = Validate(op, i, out string opName);
                if (err != null)
                    return ValidationError(i, op!, err);
                summary.Add($"Operation {i}: {opName}");
            }
            return new { success = true, mode = "preview", summary };
        }

        // ── Execute inside TransactionGroup ───────────────────────────────────

        private object ExecuteTransaction(Document doc)
        {
            var results = new List<object>();

            using var tg = new TransactionGroup(doc, "AI Apply Operations");
            tg.Start();
            try
            {
                using (var tx = new Transaction(doc, "Apply Operations"))
                {
                    tx.Start();
                    for (int i = 0; i < Operations.Count; i++)
                    {
                        var op = Operations[i] as JObject;

                        string? err = Validate(op, i, out string opName);
                        if (err != null)
                        {
                            tx.RollBack(); tg.RollBack();
                            return ValidationError(i, op!, err, rolledBack: true);
                        }

                        OperationHandlerRegistry.TryGet(opName, out var handler);
                        OperationResult r = handler!.Execute(doc, op!);

                        if (!r.Success)
                        {
                            tx.RollBack(); tg.RollBack();
                            return new
                            {
                                success = false,
                                stage = "revit_transaction",
                                failedOperationIndex = i,
                                failedOperation = op,
                                message = r.Message,
                                rolledBack = true
                            };
                        }

                        results.Add(new { success = true, message = r.Message, elementId = r.ElementId });
                    }
                    tx.Commit();
                }
                tg.Assimilate();
            }
            catch (Exception ex)
            {
                try { tg.RollBack(); } catch { /* already rolled back */ }
                return new { success = false, stage = "revit_transaction", message = ex.Message, rolledBack = true };
            }

            return new { success = true, mode = "execute", results };
        }

        // ── Validation (uses registry — no hardcoded op list) ─────────────────

        private static string? Validate(JObject? op, int index, out string opName)
        {
            opName = "";
            if (op == null)
                return "Operation is not a valid object.";

            opName = op["op"]?.ToString() ?? "";
            if (string.IsNullOrWhiteSpace(opName))
                return "Missing field 'op'.";

            if (!OperationHandlerRegistry.TryGet(opName, out var handler))
                return $"Unknown operation: '{opName}'. Available: {string.Join(", ", OperationHandlerRegistry.KnownOps)}";

            foreach (var field in handler.RequiredFields)
                if (op[field] == null)
                    return $"{opName} requires field '{field}'.";

            return null;
        }

        private static object ValidationError(int index, object op, string message, bool rolledBack = false) => new
        {
            success = false,
            stage = "validation",
            failedOperationIndex = index,
            failedOperation = op,
            message,
            rolledBack
        };
    }
}
