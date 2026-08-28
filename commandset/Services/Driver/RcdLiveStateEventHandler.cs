using Autodesk.Revit.UI;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Utils;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Driver
{
    /// <summary>
    /// Optional live probe used by rcd_ui_state(includeLiveMapping=true). Whether this handler runs
    /// while a Revit command is active is exactly spike S1 — the caller reports the outcome.
    /// </summary>
    public class RcdLiveStateEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public object Result { get; private set; }
        public DateTime RequestedUtc { get; private set; }

        public void Reset() { Result = null; RequestedUtc = DateTime.UtcNow; _resetEvent.Reset(); }
        public bool WaitForCompletion(int timeoutMilliseconds = 500) => _resetEvent.WaitOne(timeoutMilliseconds);

        public void Execute(UIApplication uiapp)
        {
            try
            {
                RcdRuntime.EnsureHooks(uiapp);
                var uidoc = uiapp.ActiveUIDocument;
                var view = uidoc?.ActiveView;
                object mapping = null;
                string mappingError = null;
                if (uidoc != null && view != null && ScreenMapper.Is2D(view))
                {
                    try { mapping = ScreenMapper.Capture(uidoc, view); } catch (Exception ex) { mappingError = ex.Message; }
                }
                Result = new
                {
                    ranUtc = DateTime.UtcNow,
                    latencyMs = (DateTime.UtcNow - RequestedUtc).TotalMilliseconds,
                    activeView = view == null ? null : new { id = view.Id.GetValue(), name = view.Name, viewType = view.ViewType.ToString() },
                    selectionCount = uidoc?.Selection.GetElementIds().Count ?? 0,
                    mapping,
                    mappingError
                };
            }
            catch (Exception ex)
            {
                Result = new { error = ex.Message };
            }
            finally { _resetEvent.Set(); }
        }

        public string GetName() => "RcdLiveStateEventHandler";
    }
}
