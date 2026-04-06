using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public interface IToolExecutionService
    {
        object Execute(UIApplication uiApp, string methodName, JObject parameters);
    }
}
