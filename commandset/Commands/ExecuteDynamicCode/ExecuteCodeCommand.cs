using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.ExecuteDynamicCode
{
    /// <summary>
    /// Command class for executing dynamic code.
    /// </summary>
    public class ExecuteCodeCommand : ExternalEventCommandBase
    {
        private ExecuteCodeEventHandler _handler => (ExecuteCodeEventHandler)Handler;

        public override string CommandName => "send_code_to_revit";

        public ExecuteCodeCommand(UIApplication uiApp)
            : base(new ExecuteCodeEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            try
            {
                if (!parameters.ContainsKey("code"))
                    throw new ArgumentException("Missing required parameter: 'code'");

                string code = parameters["code"].Value<string>();
                JArray parametersArray = parameters["parameters"] as JArray;
                object[] executionParameters = parametersArray?.ToObject<object[]>() ?? Array.Empty<object>();

                _handler.SetExecutionParameters(code, executionParameters);

                if (RaiseAndWaitForCompletion(60000)) // 1 min timeout
                    return _handler.ResultInfo;
                throw new TimeoutException("Code execution timed out.");
            }
            catch (Exception ex)
            {
                throw new Exception($"Execute code failed: {ex.Message}", ex);
            }
        }
    }
}
