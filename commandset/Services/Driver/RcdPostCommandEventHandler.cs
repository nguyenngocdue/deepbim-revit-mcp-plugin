using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Driver
{
    /// <summary>
    /// Prepare → capture mapping → mark → PostCommand. Runs on the Revit main thread; the posted
    /// command starts the moment this handler returns (end of API context).
    /// </summary>
    public class RcdPostCommandEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public JObject Parameters { get; set; } = new JObject();
        public object Result { get; private set; }

        public void SetParameters(JObject p) { Parameters = p ?? new JObject(); Result = null; _resetEvent.Reset(); }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000) => _resetEvent.WaitOne(timeoutMilliseconds);

        public void Execute(UIApplication uiapp)
        {
            var warnings = new List<string>();
            try
            {
                RcdRuntime.AssertEnabled();
                RcdRuntime.EnsureHooks(uiapp);
                CommandCatalog.EnsureBuilt(uiapp);

                string commandRef = Parameters["command"]?.ToString();
                string expect = (Parameters["expect"]?.ToString() ?? "unknown").ToLowerInvariant();
                string token = Parameters["lockToken"]?.ToString();
                int lockTtl = Parameters["lockTtlMs"]?.Value<int>() ?? RcdRuntime.Setting("lockTtlMs", 120000);
                var prepare = Parameters["prepare"] as JObject ?? new JObject();

                var (cmdId, info) = CommandCatalog.Resolve(commandRef);
                if (expect == "unknown" && info.Interaction != "unknown") expect = info.Interaction;

                var uidoc = uiapp.ActiveUIDocument ?? throw new DriverException(RcdErrorCodes.CannotPost, "No active document.");
                var doc = uidoc.Document;

                // Lock the driver for interactive commands (anything that leaves Revit waiting for input).
                if (expect != "instant") DriverLock.Acquire(token, lockTtl);
                else DriverLock.AssertOwnerOrFree(token);

                // ── prepare: active view ──
                if (prepare["activeViewId"] != null)
                {
                    long vid = prepare["activeViewId"].Value<long>();
                    var view = doc.GetElement(MakeId(vid)) as View
                               ?? throw new DriverException(RcdErrorCodes.InvalidArgument, $"activeViewId {vid} is not a view.");
                    if (view.Id != uidoc.ActiveView.Id)
                    {
                        uidoc.ActiveView = view;   // synchronous in API context
                    }
                }
                var activeView = uidoc.ActiveView;

                // ── prepare: selection ──
                if (prepare["clearSelection"]?.Value<bool>() == true)
                    uidoc.Selection.SetElementIds(new List<ElementId>());
                var selIds = prepare["selectElementIds"]?.ToObject<List<long>>();
                if (selIds != null && selIds.Count > 0)
                {
                    var ids = new List<ElementId>();
                    foreach (var v in selIds)
                    {
                        var id = MakeId(v);
                        if (doc.GetElement(id) != null) ids.Add(id); else warnings.Add($"selectElementIds: element {v} not found, skipped");
                    }
                    uidoc.Selection.SetElementIds(ids);
                }

                // ── prepare: default type (needs a transaction) ──
                if (prepare["defaultType"] is JObject dt)
                {
                    long typeId = dt["typeId"]?.Value<long>() ?? throw new DriverException(RcdErrorCodes.InvalidArgument, "defaultType.typeId is required.");
                    var typeElemId = MakeId(typeId);
                    if (doc.GetElement(typeElemId) is not ElementType)
                        throw new DriverException(RcdErrorCodes.InvalidArgument, $"defaultType.typeId {typeId} is not an ElementType.");
                    using (var t = new Transaction(doc, "RCD default type"))
                    {
                        t.Start();
                        if (dt["group"] != null)
                        {
                            if (!Enum.TryParse(dt["group"].ToString(), true, out ElementTypeGroup group))
                                throw new DriverException(RcdErrorCodes.InvalidArgument, $"defaultType.group '{dt["group"]}' is not an ElementTypeGroup (e.g. WallType, FloorType, RoofType, CeilingType, TextNoteType).");
                            doc.SetDefaultElementTypeId(group, typeElemId);
                        }
                        else if (dt["categoryId"] != null)
                        {
                            var catId = MakeId(dt["categoryId"].Value<long>());
                            doc.SetDefaultFamilyTypeId(catId, typeElemId);
                        }
                        else throw new DriverException(RcdErrorCodes.InvalidArgument, "defaultType needs 'group' (system families) or 'categoryId' (loadable families).");
                        t.Commit();
                    }
                }

                // ── mapping (2D views only) ──
                ViewMapping mapping = null;
                bool needsMapping = expect == "points" || expect == "sketch" || expect == "unknown";
                var fit = prepare["fitPoints"]?.ToObject<List<double[]>>();
                if (ScreenMapper.Is2D(activeView))
                {
                    try
                    {
                        double pad = prepare["fitPaddingMm"]?.Value<double>() ?? 1500;
                        double maxMmPx = prepare["maxMmPerPixel"]?.Value<double>() ?? RcdRuntime.Setting("maxMmPerPixel", 5.0);
                        mapping = ScreenMapper.FitAndCapture(uidoc, activeView, fit, pad, maxMmPx, warnings);
                        if (maxMmPx > 0 && mapping.MmPerPixel > maxMmPx && (fit == null || fit.Count == 0))
                            warnings.Add($"mmPerPixel {mapping.MmPerPixel} > maxMmPerPixel {maxMmPx}: pass prepare.fitPoints to zoom in for precise clicks.");
                    }
                    catch (DriverException dex) when (!needsMapping) { warnings.Add("mapping skipped: " + dex.Message); }
                }
                else if (needsMapping && expect == "points")
                {
                    throw new DriverException(RcdErrorCodes.ViewNot2D, $"Active view '{activeView.Name}' is {activeView.ViewType}. Interactive drawing needs a plan/section/elevation/drafting view — pass prepare.activeViewId.",
                        new { activeViewType = activeView.ViewType.ToString() });
                }
                else warnings.Add($"Active view is {activeView.ViewType}; no screen mapping captured.");

                // ── can post? ──
                bool canPost;
                try { canPost = uiapp.CanPostCommand(cmdId); }
                catch (Exception ex) { throw new DriverException(RcdErrorCodes.CannotPost, $"CanPostCommand threw: {ex.Message}"); }
                if (!canPost)
                    throw new DriverException(RcdErrorCodes.CannotPost, $"Revit reports '{info.Name}' ({cmdId.Name}) cannot be posted right now (active view {activeView.ViewType}, selection {uidoc.Selection.GetElementIds().Count}). Change view/selection and retry.",
                        new { activeViewType = activeView.ViewType.ToString(), selectionCount = uidoc.Selection.GetElementIds().Count });

                string statusBefore = StatusBarReader.ReadText();
                long marker = ChangeTracker.Mark();

                // ── post ──
                try
                {
                    uiapp.PostCommand(cmdId);
                }
                catch (Autodesk.Revit.Exceptions.InvalidOperationException ex)
                {
                    throw new DriverException(RcdErrorCodes.PostPending, $"PostCommand rejected: {ex.Message} (a previously posted command has not run yet, or Revit is in a state that blocks posting). Call rcd_ui_cancel and retry.");
                }

                DriverLock.SetSession(mapping, marker, info.Name);
                RcdRuntime.Log($"Posted {info.Name} ({cmdId.Name}) expect={expect} view={activeView.Name} marker={marker} mapping={(mapping == null ? "none" : mapping.MmPerPixel + "mm/px")}");

                Result = RcdRuntime.Ok(new
                {
                    posted = true,
                    command = new { name = info.Name, id = cmdId.Name, kind = info.Kind, interaction = info.Interaction, expect },
                    marker,
                    statusBefore,
                    activeView = new { id = activeView.Id.GetValue(), name = activeView.Name, viewType = activeView.ViewType.ToString() },
                    selectionCount = uidoc.Selection.GetElementIds().Count,
                    mapping,
                    lockToken = DriverLock.Normalize(token),
                    warnings,
                    next = expect == "points" || expect == "sketch"
                        ? "Revit is now in the command and waiting for input. Call rcd_ui_input starting with a waitStatus step, then click/type, and finish with key Escape ×2."
                        : expect == "dialog"
                            ? "Revit should now show a dialog. Call rcd_ui_state to read it; use rcd_dialog_policy or rcd_ui_cancel to respond."
                            : "The command executes immediately after this response. Call rcd_ui_state with sinceMarker to see resulting changes."
                });
            }
            catch (Exception ex)
            {
                Result = RcdRuntime.FailFromException(ex);
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        private static ElementId MakeId(long v)
        {
#if REVIT2024_OR_GREATER
            return new ElementId(v);
#else
            return new ElementId((int)v);
#endif
        }

        public string GetName() => "RcdPostCommandEventHandler";
    }
}
