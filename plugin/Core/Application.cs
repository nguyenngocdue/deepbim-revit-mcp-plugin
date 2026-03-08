using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using revit_mcp_plugin.Utils;

namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        public Result OnStartup(UIControlledApplication application)
        {
            // Record add-in directory on load (avoids Assembly.Location empty/temp when opening Settings)
            try
            {
                string loc = Assembly.GetExecutingAssembly().Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        PathManager.SetPluginDirectory(dir);
                }
            }
            catch { }

            RibbonPanel mcpPanel = application.CreateRibbonPanel("DeepBim-MCP");

            PushButtonData toggleButton = new PushButtonData(
                "ID_TOGGLE_MCP",
                "MCP\r\nSwitch",
                Assembly.GetExecutingAssembly().Location,
                "revit_mcp_plugin.Core.MCPServiceConnection");
            toggleButton.ToolTip = "Start / Stop MCP server";
            mcpPanel.AddItem(toggleButton);

            PushButtonData settingsButton = new PushButtonData(
                "ID_MCP_SETTINGS",
                "Settings",
                Assembly.GetExecutingAssembly().Location,
                "revit_mcp_plugin.Core.Settings");
            settingsButton.ToolTip = "MCP Plugin Settings";
            mcpPanel.AddItem(settingsButton);

            return Result.Succeeded;
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                if (SocketService.Instance.IsRunning)
                {
                    SocketService.Instance.Stop();
                }
            }
            catch { }

            return Result.Succeeded;
        }
    }
}
