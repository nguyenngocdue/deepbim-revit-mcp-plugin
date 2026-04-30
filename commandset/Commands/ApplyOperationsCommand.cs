using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands
{
    public class ApplyOperationsCommand : ExternalEventCommandBase
    {
        private Services.ApplyOperationsEventHandler _handler =>
            (Services.ApplyOperationsEventHandler)Handler;

        public override string CommandName => "apply_operations";

        public ApplyOperationsCommand(UIApplication uiApp)
            : base(new Services.ApplyOperationsEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            string mode = parameters["mode"]?.ToString() ?? "execute";
            var operations = parameters["operations"] as JArray ?? new JArray();

            _handler.SetParameters(mode, operations);

            // Allow up to 60s for large operation batches
            if (RaiseAndWaitForCompletion(60000))
            {
                return _handler.Result;
            }
            throw new TimeoutException("apply_operations timed out.");
        }
    }
}
