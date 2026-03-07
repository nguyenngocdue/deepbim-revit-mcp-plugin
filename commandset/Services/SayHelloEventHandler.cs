using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class SayHelloEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public AIResult<string> Result { get; private set; }
    public string UserName { get; set; } = "World";

    public void SetParameters(string name)
    {
        UserName = string.IsNullOrEmpty(name) ? "World" : name;
        _resetEvent.Reset();
    }

    public void Execute(UIApplication uiapp)
    {
        try
        {
            string greeting = $"Hello, {UserName}! DeepBim-MCP is working!";
            TaskDialog.Show("DeepBim-MCP", greeting);

            Result = new AIResult<string>
            {
                Success = true,
                Message = greeting,
                Response = greeting
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<string>
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
        finally
        {
            _resetEvent.Set();
        }
    }

    public bool WaitForCompletion(int timeoutMilliseconds = 10000)
    {
        _resetEvent.Reset();
        return _resetEvent.WaitOne(timeoutMilliseconds);
    }

    public string GetName() => "SayHello";
}
