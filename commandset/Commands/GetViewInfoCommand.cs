using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands;

public class GetViewInfoCommand : ExternalEventCommandBase
{
    private GetViewInfoEventHandler _handler => (GetViewInfoEventHandler)Handler;

    public override string CommandName => "get_view_info";

    public GetViewInfoCommand(UIApplication uiApp)
        : base(new GetViewInfoEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        if (RaiseAndWaitForCompletion(10000))
        {
            return _handler.Result;
        }
        throw new TimeoutException("Get view info timed out.");
    }
}
