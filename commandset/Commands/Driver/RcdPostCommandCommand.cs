using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>rcd_post_command — prepare context, capture view mapping, PostCommand.</summary>
    public class RcdPostCommandCommand : ExternalEventCommandBase
    {
        private Services.Driver.RcdPostCommandEventHandler _handler => (Services.Driver.RcdPostCommandEventHandler)Handler;

        public override string CommandName => "rcd_post_command";

        public RcdPostCommandCommand(UIApplication uiApp) : base(new Services.Driver.RcdPostCommandEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            _handler.SetParameters(parameters);
            if (RaiseAndWaitForCompletion(15000))
                return _handler.Result;
            return RcdRuntime.Fail(RcdErrorCodes.ExternalEventTimeout,
                "Revit did not process the post request within 15 s — it is probably still inside a previous interactive command or a modal dialog. Call rcd_ui_state to inspect and rcd_ui_cancel to recover.",
                new { status = StatusBarReader.Read(), dialogs = WindowProbe.FindDialogs(), driver = DriverLock.Snapshot() });
        }
    }
}
