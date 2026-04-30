using Autodesk.Revit.UI;
using RevitMCPCommandSet.Models;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services
{
    public class GetRevitContextEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public object Result { get; private set; }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000)
        {
            _resetEvent.Reset();
            return _resetEvent.WaitOne(timeoutMilliseconds);
        }

        public void Execute(UIApplication uiapp)
        {
            try
            {
                var uiDoc = uiapp.ActiveUIDocument;
                var doc = uiDoc.Document;
                var activeView = doc.ActiveView;

                // --- Document ---
                var docInfo = new
                {
                    title = doc.Title,
                    pathName = doc.PathName,
                    isWorkshared = doc.IsWorkshared
                };

                // --- Active view ---
                var viewInfo = new
                {
#if REVIT2024_OR_GREATER
                    id = (int)activeView.Id.Value,
#else
                    id = activeView.Id.IntegerValue,
#endif
                    name = activeView.Name,
                    viewType = activeView.ViewType.ToString(),
                    scale = activeView.Scale
                };

                // --- Units ---
                var units = doc.GetUnits();
                var lengthFormatOptions = units.GetFormatOptions(SpecTypeId.Length);
                var unitsInfo = new
                {
                    length = lengthFormatOptions.GetUnitTypeId().TypeId,
                    internalLength = "feet"
                };

                // --- Selection ---
                var selectedIds = uiDoc.Selection.GetElementIds()
                    .Select(id =>
#if REVIT2024_OR_GREATER
                        (int)id.Value
#else
                        id.IntegerValue
#endif
                    )
                    .ToList();

                var selectionInfo = new
                {
                    count = selectedIds.Count,
                    selectedElementIds = selectedIds
                };

                // --- Levels ---
                var levels = new FilteredElementCollector(doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .OrderBy(l => l.Elevation)
                    .Select(l => new
                    {
#if REVIT2024_OR_GREATER
                        id = (int)l.Id.Value,
#else
                        id = l.Id.IntegerValue,
#endif
                        name = l.Name,
                        elevation = Math.Round(l.Elevation * 304.8, 3)
                    })
                    .ToList();

                // --- Wall types ---
                var wallTypes = new FilteredElementCollector(doc)
                    .OfClass(typeof(WallType))
                    .Cast<WallType>()
                    .Select(wt => new
                    {
#if REVIT2024_OR_GREATER
                        id = (int)wt.Id.Value,
#else
                        id = wt.Id.IntegerValue,
#endif
                        name = wt.Name,
                        familyName = wt.FamilyName,
                        width = Math.Round(wt.Width * 304.8, 3)
                    })
                    .ToList();

                Result = new AIResult<object>
                {
                    Success = true,
                    Response = new
                    {
                        document = docInfo,
                        activeView = viewInfo,
                        units = unitsInfo,
                        selection = selectionInfo,
                        levels,
                        types = new { wallTypes }
                    }
                };
            }
            catch (Exception ex)
            {
                Result = new AIResult<object>
                {
                    Success = false,
                    Message = ex.Message
                };
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "GetRevitContextEventHandler";
    }
}
