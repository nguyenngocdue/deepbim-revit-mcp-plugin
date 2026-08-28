using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver;
using RevitMCPSDK.API.Interfaces;

namespace RevitMCPCommandSet.Services.Driver
{
    /// <summary>Builds (once) and searches the PostableCommand catalog. Runs on the Revit main thread.</summary>
    public class RcdListCommandsEventHandler : IExternalEventHandler, IWaitableExternalEventHandler
    {
        private readonly ManualResetEvent _resetEvent = new ManualResetEvent(false);

        public JObject Parameters { get; set; } = new JObject();
        public object Result { get; private set; }

        public void SetParameters(JObject p) { Parameters = p ?? new JObject(); Result = null; _resetEvent.Reset(); }

        public bool WaitForCompletion(int timeoutMilliseconds = 15000) => _resetEvent.WaitOne(timeoutMilliseconds);

        public void Execute(UIApplication uiapp)
        {
            try
            {
                RcdRuntime.AssertEnabled();
                RcdRuntime.EnsureHooks(uiapp);
                if (Parameters["refresh"]?.Value<bool>() == true) CommandCatalog.Invalidate();
                CommandCatalog.EnsureBuilt(uiapp);

                string query = Parameters["query"]?.ToString();
                var tags = Parameters["tags"]?.ToObject<List<string>>();
                int limit = Parameters["limit"]?.Value<int>() ?? 50;
                limit = Math.Max(1, Math.Min(limit, 500));
                bool onlyPostable = Parameters["onlyPostable"]?.Value<bool>() ?? false;
                bool includeCanPost = Parameters["includeCanPost"]?.Value<bool>() ?? true;

                // Search a wider window when filtering on canPost so the limit still fills up.
                var items = CommandCatalog.Search(query, tags, onlyPostable ? limit * 4 : limit);
                if (includeCanPost || onlyPostable)
                {
                    foreach (var it in items)
                    {
                        try
                        {
                            var resolved = CommandCatalog.Resolve(it.Name);
                            it.CanPost = uiapp.CanPostCommand(resolved.id);
                        }
                        catch { it.CanPost = null; }
                    }
                    if (onlyPostable) items = items.Where(i => i.CanPost == true).Take(limit).ToList();
                }

                string activeViewType = null, activeViewName = null;
                try { activeViewType = uiapp.ActiveUIDocument?.ActiveView?.ViewType.ToString(); activeViewName = uiapp.ActiveUIDocument?.ActiveView?.Name; } catch { }

                Result = RcdRuntime.Ok(new
                {
                    revitVersion = RcdRuntime.RevitVersion,
                    catalogSize = CommandCatalog.Count,
                    shortcutsFile = CommandCatalog.ShortcutsSource,
                    shortcutsMatched = CommandCatalog.ShortcutsMatched,
                    activeView = new { name = activeViewName, viewType = activeViewType },
                    total = items.Count,
                    items
                });
            }
            catch (Exception ex)
            {
                Result = RcdRuntime.FailFromException(ex);
            }
            finally
            {
                _resetEvent.Set();
            }
        }

        public string GetName() => "RcdListCommandsEventHandler";
    }
}
