using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>
    /// rcd_ui_input — drive mouse/keyboard against the view mapping captured by the last
    /// rcd_post_command. Direct path: runs on the socket thread, no ExternalEvent, no Revit API.
    /// </summary>
    public class RcdUiInputCommand : IRevitCommand
    {
        public string CommandName => "rcd_ui_input";

        public RcdUiInputCommand(UIApplication uiApp) { }

        public object Execute(JObject parameters, string requestId)
        {
            try
            {
                RcdRuntime.AssertEnabled();
                string token = parameters["lockToken"]?.ToString();
                DriverLock.AssertOwnerOrFree(token);

                var steps = parameters["steps"]?.ToObject<List<InputStep>>();
                if (steps == null || steps.Count == 0)
                    throw new DriverException(RcdErrorCodes.InvalidArgument, "steps[] is required.");
                if (steps.Count > 200)
                    throw new DriverException(RcdErrorCodes.InvalidArgument, "Too many steps (max 200 per batch).");

                var opt = new InputDriver.BatchOptions
                {
                    StopOnDialog = parameters["stopOnDialog"]?.Value<bool>() ?? true,
                    StopOnStatusMismatch = parameters["stopOnStatusMismatch"]?.Value<bool>() ?? false,
                    InterStepDelayMs = parameters["interStepDelayMs"]?.Value<int>() ?? RcdRuntime.Setting("interStepDelayMs", 60),
                    DryRun = parameters["dryRun"]?.Value<bool>() ?? false
                };

                long marker = parameters["sinceMarker"]?.Value<long>() ?? DriverLock.Marker;
                var mapping = DriverLock.Mapping;
                if (!opt.DryRun) DriverLock.Touch(RcdRuntime.Setting("lockTtlMs", 120000));

                RcdRuntime.Log($"ui_input start: {steps.Count} steps dryRun={opt.DryRun} mapping={(mapping == null ? "none" : mapping.ViewName + " " + mapping.MmPerPixel + "mm/px")}");
                var driver = new InputDriver(mapping, opt);
                var res = driver.Run(steps, marker);
                var changes = ChangeTracker.Since(marker, 200);

                object payload = new
                {
                    completed = res.Completed,
                    totalSteps = steps.Count,
                    dryRun = opt.DryRun,
                    steps = res.Steps,
                    statusFinal = res.StatusFinal,
                    idle = res.Idle,
                    dialog = res.Dialog,
                    changes,
                    foreground = WindowProbe.Foreground(),
                    mapping = mapping == null ? null : new { mapping.ViewName, mapping.MmPerPixel, ageSec = Math.Round((DateTime.UtcNow - mapping.CapturedUtc).TotalSeconds, 1) },
                    marker
                };

                if (res.ErrorCode != null)
                    return RcdRuntime.Fail(res.ErrorCode, res.Error, payload);
                return RcdRuntime.Ok(payload);
            }
            catch (Exception ex)
            {
                return RcdRuntime.FailFromException(ex);
            }
        }
    }
}
