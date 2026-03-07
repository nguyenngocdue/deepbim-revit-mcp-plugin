using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class GetViewInfoEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public ViewInfoResult Result { get; private set; }

    public void Execute(UIApplication uiapp)
    {
        try
        {
            var doc = uiapp.ActiveUIDocument.Document;
            var activeView = doc.ActiveView;

            Result = new ViewInfoResult
            {
                Id = (int)activeView.Id.Value,
                UniqueId = activeView.UniqueId,
                Name = activeView.Name,
                ViewType = activeView.ViewType.ToString(),
                IsTemplate = activeView.IsTemplate,
                Scale = activeView.Scale,
                DetailLevel = activeView.DetailLevel.ToString(),
            };
        }
        catch (Exception ex)
        {
            Result = null;
            TaskDialog.Show("Error", $"Failed to get view info: {ex.Message}");
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

    public string GetName() => "GetViewInfo";
}
