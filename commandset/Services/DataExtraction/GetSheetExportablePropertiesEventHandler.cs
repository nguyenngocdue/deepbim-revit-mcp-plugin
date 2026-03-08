using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using RevitMCPSDK.API.Interfaces;
using System.Collections.Generic;
using System.Linq;

namespace RevitMCPCommandSet.Services.DataExtraction
{
    public class GetSheetExportablePropertiesEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);
        public List<SheetPropertyInfo> Result { get; private set; }
        public bool TaskCompleted { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 10000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication app)
        {
            try
            {
                var doc = app.ActiveUIDocument?.Document;
                if (doc == null)
                {
                    Result = new List<SheetPropertyInfo>();
                    return;
                }

                var sheets = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType()
                    .ToElements();

                var paramNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (Element el in sheets)
                {
                    foreach (Parameter p in el.GetOrderedParameters())
                    {
                        if (p == null) continue;
                        string name = p.Definition?.Name;
                        if (!string.IsNullOrEmpty(name))
                            paramNames.Add(name);
                    }
                }

                Result = paramNames
                    .OrderBy(s => s)
                    .Select(name => new SheetPropertyInfo { Name = name })
                    .ToList();
            }
            catch (System.Exception)
            {
                Result = new List<SheetPropertyInfo>();
            }
            finally
            {
                TaskCompleted = true;
                _resetEvent.Set();
            }
        }

        public string GetName() => "Get Sheet Exportable Properties";
    }

    public class SheetPropertyInfo
    {
        public string Name { get; set; }
    }
}
