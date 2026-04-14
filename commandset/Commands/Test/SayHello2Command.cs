using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;

namespace RevitMCPCommandSet.Commands.Test
{
    public class SayHello2Command : ExternalEventCommandBase
    {
        // 
        private static readonly object _executionLock = new object();

        private SayHelloEventHandler _hanlder => (SayHelloEventHandler)Handler;

        public override string CommandName => "say_hello";

        public SayHello2Command(UIApplication uiApp) : base(new SayHelloEventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
               try
                {
                    // Parse optional message parameter
                    string message = "Hello MCP!";
                    if (parameters?["parameter"] != null)
                    {
                        message = parameters["parameter"].ToString();
                    }
                    _hanlder.Message = message;
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return new { success = true, message = $"Said: {message}" };
                    }
                    else
                    {
                        throw new Exception("Say hello operation timed out.");
                    }
                }
                catch (Exception ex) { 
                    throw new Exception($"Say hello failed: {CommandName}: {ex.Message}", ex);
                }
            }

        }

    }
}
