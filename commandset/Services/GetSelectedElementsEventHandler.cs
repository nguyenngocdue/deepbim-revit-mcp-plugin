using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models;
using RevitMCPSDK.API.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace RevitMCPCommandSet.Services;

public class GetSelectedElementsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
{
    private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

    public AIResult<List<ElementInfo>> Result { get; private set; }
    public int? Limit { get; set; }

    public void Execute(UIApplication uiapp)
    {
        try
        {
            var uiDoc = uiapp.ActiveUIDocument;
            var doc = uiDoc.Document;
            var selectedIds = uiDoc.Selection.GetElementIds();

            var elements = new List<ElementInfo>();

            IEnumerable<ElementId> ids = selectedIds.AsEnumerable();
            if (Limit.HasValue && Limit.Value > 0)
                ids = ids.Take(Limit.Value);

            foreach (var id in ids)
            {
                var element = doc.GetElement(id);
                if (element == null) continue;

                var info = new ElementInfo
                {
                    Id = (int)id.Value,
                    UniqueId = element.UniqueId,
                    Name = element.Name,
                    Category = element.Category?.Name ?? "Unknown",
                };

                if (element is FamilyInstance fi)
                {
                    info.FamilyName = fi.Symbol?.FamilyName;
                    info.TypeName = fi.Symbol?.Name;
                }

                elements.Add(info);
            }

            Result = new AIResult<List<ElementInfo>>
            {
                Success = true,
                Message = $"Found {elements.Count} selected element(s).",
                Response = elements
            };
        }
        catch (Exception ex)
        {
            Result = new AIResult<List<ElementInfo>>
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                Response = new List<ElementInfo>()
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

    public string GetName() => "GetSelectedElements";
}
