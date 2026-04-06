using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public sealed class ToolExecutionService : IToolExecutionService
    {
        private readonly ICommandGateway _gateway;

        public ToolExecutionService(ICommandGateway gateway)
        {
            _gateway = gateway;
        }

        public object Execute(UIApplication uiApp, string methodName, JObject parameters)
        {
            return _gateway.Invoke(uiApp, methodName, parameters);
        }
    }
}
