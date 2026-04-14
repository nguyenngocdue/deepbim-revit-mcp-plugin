using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    /**
     * This event handler demonstrates a more complex interaction where the command logic is separated into a ViewModel.
     * The command creates a SayHelloModel from the input parameters, runs the business logic in SayHelloViewModel, and then
     * passes the result to this event handler for display. This pattern allows for better separation of concerns and easier testing.
     * IExternalEventHandler: Defines the contract for handling external events in Revit.
     * IWaitableExternalEventHandler: Extends IExternalEventHandler to include waiting functionality, allowing the command to wait for the event handler to complete its execution before proceeding.
     */
    public class SayHelloV2EventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public string Message { get; set; } = "Hello from V2!";

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                TaskDialog.Show("Revit MCP V2", Message);
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName()
        {
            return "Say Hello V2";
        }
    }
}
