using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Driver
{
    /// <summary>rcd_list_commands — search the catalog of postable Revit commands.</summary>
    public class RcdListCommandsCommand : ExternalEventCommandBase
    {
        private Services.Driver.RcdListCommandsEventHandler _handler => (Services.Driver.RcdListCommandsEventHandler)Handler;

        public override string CommandName => "rcd_list_commands";

        public RcdListCommandsCommand(UIApplication uiApp) : base(new Services.Driver.RcdListCommandsEventHandler(), uiApp) { }

        public override object Execute(JObject parameters, string requestId)
        {
            _handler.SetParameters(parameters);
            if (RaiseAndWaitForCompletion(20000))
                return _handler.Result;
            return RcdRuntime.Fail(RcdErrorCodes.ExternalEventTimeout,
                "Revit did not process the request within 20 s (busy in a command or a modal dialog). Call rcd_ui_state / rcd_ui_cancel.");
        }
    }
}
