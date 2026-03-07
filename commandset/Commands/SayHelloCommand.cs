using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands;

public class SayHelloCommand : ExternalEventCommandBase
{
    private SayHelloEventHandler _handler => (SayHelloEventHandler)Handler;

    public override string CommandName => "say_hello";

    public SayHelloCommand(UIApplication uiApp)
        : base(new SayHelloEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            string name = parameters?["name"]?.Value<string>() ?? "World";
            _handler.SetParameters(name);

            if (RaiseAndWaitForCompletion(10000))
            {
                return _handler.Result;
            }
            throw new TimeoutException("Say hello operation timed out.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Say hello failed: {ex.Message}");
        }
    }
}
