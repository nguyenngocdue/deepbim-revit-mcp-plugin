using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class GetRevitContextCommand : ExternalEventCommandBase
    {
        private Services.GetRevitContextEventHandler _handler =>
            (Services.GetRevitContextEventHandler)Handler;

        public override string CommandName => "get_revit_context";

        public GetRevitContextCommand(UIApplication uiApp)
            : base(new Services.GetRevitContextEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            if (RaiseAndWaitForCompletion(15000))
            {
                return _handler.Result;
            }
            throw new TimeoutException("get_revit_context timed out.");
        }
    }
}
