using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class HelloWorldEventHandler: IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public string FullName { get; set; } = string.Empty;
        public string Message { get; set; } = "Hello World from MCP";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                string displayMessage = $"{Message} (from {FullName})";
                TaskDialog.Show("Hello World Revit MCP", displayMessage);
            } finally
            {
                _resetEvent.Set();
            }
           
        }

        public string GetName()
        {
            return "Hello World";
        }
    }
}
