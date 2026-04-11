using Autodesk.Revit.UI;
using DevTooV2lCommands.Models;
using DevTooV2lCommands.ViewModes;
using Newtonsoft.Json.Linq;
using RevitMCPSDK.API.Base;
using RevitMCPCommandSet.Services;

namespace RevitMCPCommandSet.Commands.Test
{
    public class SayHelloV2Command : ExternalEventCommandBase
    {
        private static readonly object _executionLock = new object();
        private SayHelloV2EventHandler _handler => (SayHelloV2EventHandler)Handler;

        public override string CommandName => "say_hello_v2";

        public SayHelloV2Command(UIApplication uiApp)
            : base(new SayHelloV2EventHandler(), uiApp)
        {
        }

        public override object Execute(JObject parameters, string requestId)
        {
            lock (_executionLock)
            {
                try
                {
                    // 1. Tạo model từ parameters
                    var model = new SayHelloModel
                    {
                        Message = parameters?["message"]?.ToString() ?? "Hello from V2!"
                    };

                    // 2. Chạy business logic từ DevToolV2Commands
                    var viewModel = new SayHelloViewModel(model);
                    viewModel.Run();

                    if (!viewModel.IsSuccess)
                        throw new Exception(viewModel.Result);

                    // 3. Truyền kết quả sang Handler để hiển thị trên Revit UI
                    _handler.Message = viewModel.Result;

                    if (RaiseAndWaitForCompletion(15000))
                    {
                        return new { success = true, message = viewModel.Result };
                    }
                    else
                    {
                        throw new TimeoutException("say_hello_v2 operation timed out");
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"say_hello_v2 failed: {ex.Message}");
                }
            }
        }
    }
}
