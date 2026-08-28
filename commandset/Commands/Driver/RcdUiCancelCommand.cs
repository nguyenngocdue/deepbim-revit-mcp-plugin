using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>
    /// rcd_ui_cancel — abort any running batch, press Escape, close stray dialogs, release the lock.
    /// Direct path (no Revit API) so it works even when Revit is modal.
    /// </summary>
    public class RcdUiCancelCommand : IRevitCommand
    {
        public string CommandName => "rcd_ui_cancel";

        public RcdUiCancelCommand(UIApplication uiApp) { }

        public object Execute(JObject parameters, string requestId)
        {
            try
            {
                string token = parameters["lockToken"]?.ToString();
                int escapes = parameters["escapes"]?.Value<int>() ?? 3;
                string closeDialogs = (parameters["closeDialogs"]?.ToString() ?? "cancel").ToLowerInvariant();
                bool releaseLock = parameters["releaseLock"]?.Value<bool>() ?? true;
                bool useSendInput = parameters["useSendInput"]?.Value<bool>() ?? true;

                DriverLock.RequestAbort();
                Thread.Sleep(50);

                var closed = new List<string>();
                if (closeDialogs != "none")
                {
                    foreach (var d in WindowProbe.FindDialogs())
                    {
                        var h = (IntPtr)d.Hwnd;
                        bool done = false;
                        if (closeDialogs == "cancel") done = WindowProbe.ClickButton(h, "Cancel") || WindowProbe.ClickButton(h, "No") || WindowProbe.ClickButton(h, "Close");
                        else if (closeDialogs == "ok") done = WindowProbe.ClickButton(h, "OK") || WindowProbe.ClickButton(h, "Yes");
                        if (!done) WindowProbe.Close(h);
                        closed.Add(d.Title);
                    }
                    if (closed.Count > 0) Thread.Sleep(200);
                }

                InputDriver.SendEscape(Math.Max(0, Math.Min(escapes, 6)), useSendInput);
                Thread.Sleep(150);

                if (releaseLock) DriverLock.Release(token, force: parameters["force"]?.Value<bool>() ?? false);
                DriverLock.ClearAbort();

                var status = StatusBarReader.Read();
                RcdRuntime.Log($"ui_cancel: escapes={escapes} closed=[{string.Join(",", closed)}] status='{status.Text}' idle={status.IsIdle}");
                return RcdRuntime.Ok(new
                {
                    statusFinal = status.Text,
                    idle = status.IsIdle,
                    closedDialogs = closed,
                    remainingDialogs = WindowProbe.FindDialogs(),
                    foreground = WindowProbe.Foreground(),
                    driver = DriverLock.Snapshot()
                });
            }
            catch (Exception ex)
            {
                return RcdRuntime.FailFromException(ex);
            }
        }
    }
}
