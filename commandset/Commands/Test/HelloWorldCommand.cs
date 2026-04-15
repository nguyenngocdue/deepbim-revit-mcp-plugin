using Autodesk.Revit.UI;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using RevitMCPSDK.API.Interfaces;


namespace RevitMCPCommandSet.Commands.Test
{
    public class HelloWorldCommand : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();

        private HelloWorldEventHandler _handler => (HelloWorldEventHandler)Handler;

        public override string CommandName => "hello_world";

        public HelloWorldCommand(HelloWorldEventHandler handler, UIApplication uiApp)
      : base(handler, uiApp)
        {
        }

        public override object Execute(Newtonsoft.Json.Linq.JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    string fullName = parameters?["fullName"]?.ToString();
                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        throw new Exception("Paramter 'full name' is required!");
                    }
                    string message = parameters?["message"]?.ToString();
                    if (string.IsNullOrWhiteSpace(message))
                    {
                        message = $"Hello, {fullName}! Welcome to use MCP!";
                    }
                    _handler.Message = message;
                    _handler.FullName = fullName;
                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return new { success = true, message = message, full_name = fullName };
                    }
                    else
                    {
                        throw new Exception("Hello world operation timed out.");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Hello world command failed: {ex.Message}");
                }
            }
        }
    }
}
