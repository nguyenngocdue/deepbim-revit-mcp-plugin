using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    public interface ICommandGateway
    {
        object Invoke(UIApplication uiApp, string methodName, JObject parameters = null);
    }
}
