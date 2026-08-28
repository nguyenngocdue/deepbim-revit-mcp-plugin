using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>
    /// rcd_ui_state — status bar, foreground, dialogs, changes since marker, driver session.
    /// Answers from Win32 + buffers (no API). With includeLiveMapping=true it additionally tries a
    /// short ExternalEvent and reports whether Revit processed it (spike S1 instrument).
    /// </summary>
    public class RcdUiStateCommand : ExternalEventCommandBase
    {
        private Services.Driver.RcdLiveStateEventHandler _handler => (Services.Driver.RcdLiveStateEventHandler)Handler;

        public override string CommandName => "rcd_ui_state";

        public RcdUiStateCommand(UIApplication uiApp) : base(new Services.Driver.RcdLiveStateEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                long since = parameters["sinceMarker"]?.Value<long>() ?? DriverLock.Marker;
                int maxIds = parameters["maxIds"]?.Value<int>() ?? 200;
                bool live = parameters["includeLiveMapping"]?.Value<bool>() ?? false;
                int liveTimeout = parameters["liveTimeoutMs"]?.Value<int>() ?? 700;

                var status = StatusBarReader.Read();
                var dialogs = WindowProbe.FindDialogs();
                var changes = ChangeTracker.Since(since, maxIds);
                var driver = DriverLock.Snapshot();

                object liveResult = null;
                bool? liveApiAvailable = null;
                if (live)
                {
                    _handler.Reset();
                    bool ran = RaiseAndWaitForCompletion(Math.Max(100, Math.Min(liveTimeout, 5000)));
                    liveApiAvailable = ran;
                    liveResult = ran ? _handler.Result : new { note = $"ExternalEvent not processed within {liveTimeout} ms — Revit is busy (active command / modal dialog)." };
                }

                var mapping = DriverLock.Mapping;
                bool mappingStale = false;
                if (mapping != null)
                {
                    // Cheap staleness heuristic without API: main window title carries the active view name.
                    string title = Win32Title();
                    if (!string.IsNullOrEmpty(title) && !string.IsNullOrEmpty(mapping.ViewName) && title.IndexOf(mapping.ViewName, StringComparison.OrdinalIgnoreCase) < 0)
                        mappingStale = true;
                }

                return RcdRuntime.Ok(new
                {
                    status = new { text = status.Text, isIdle = status.IsIdle, statusBarFound = status.Found },
                    foreground = WindowProbe.Foreground(),
                    mainWindowTitle = Win32Title(),
                    dialog = dialogs.Count == 0 ? (object)new { open = false } : new { open = true, count = dialogs.Count, first = dialogs[0], all = dialogs },
                    changes,
                    dialogEvents = DialogPolicy.EventsSince(DateTime.UtcNow.AddMinutes(-10)),
                    driver,
                    mappingStale,
                    hooksInstalled = RcdRuntime.HooksInstalled,
                    liveApiAvailable,
                    live = liveResult,
                    revitVersion = RcdRuntime.RevitVersion
                });
            }
            catch (Exception ex)
            {
                return RcdRuntime.FailFromException(ex);
            }
        }

        private static string Win32Title() => RevitMCPCommandSet.Driver.Native.Win32.GetWindowTextSafe(RcdRuntime.MainHwnd);
    }
}
