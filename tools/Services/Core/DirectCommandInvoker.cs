using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;

namespace DeepBimMCPTools
{
    /// <summary>
    /// Gọi trực tiếp command trong RevitMCPCommandSet (không cần MCP server).
    /// Dùng cho panel Tools để test từng chức năng.
    /// </summary>
    public static class DirectCommandInvoker
    {
        private static readonly ICommandGateway DefaultGateway = new DirectCommandGateway();

        public static object Invoke(UIApplication uiApp, string methodName, JObject parameters = null)
        {
            return DefaultGateway.Invoke(uiApp, methodName, parameters);
        }
    }
}
