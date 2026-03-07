using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Services;
using RevitMCPSDK.API.Base;
using System;

namespace RevitMCPCommandSet.Commands;

public class GetSelectedElementsCommand : ExternalEventCommandBase
{
    private GetSelectedElementsEventHandler _handler => (GetSelectedElementsEventHandler)Handler;

    public override string CommandName => "get_selected_elements";

    public GetSelectedElementsCommand(UIApplication uiApp)
        : base(new GetSelectedElementsEventHandler(), uiApp)
    {
    }

    public override object Execute(JObject parameters, string requestId)
    {
        try
        {
            int? limit = parameters?["limit"]?.Value<int>();
            _handler.Limit = limit;

            if (RaiseAndWaitForCompletion(15000))
            {
                return _handler.Result;
            }
            throw new TimeoutException("Get selected elements timed out.");
        }
        catch (Exception ex)
        {
            throw new Exception($"Get selected elements failed: {ex.Message}");
        }
    }
}
