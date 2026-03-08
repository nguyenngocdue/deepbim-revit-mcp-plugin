using System;
using System.IO;
using System.Reflection;
using Autodesk.Revit.UI;
using revit_mcp_plugin.Utils;

namespace revit_mcp_plugin.Core
{
    public class Application : IExternalApplication
    {
        private const string TabName = "DeepBim-MCP";
        private const string PanelNameServer = "Server";
        private const string PanelNameTools = "Tools";

        public Result OnStartup(UIControlledApplication application)
        {
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

            try
            {
                application.CreateRibbonTab(TabName);
            }
            catch { /* tab may already exist */ }

            RibbonPanel mcpPanel = application.CreateRibbonPanel(TabName, PanelNameServer);

            PushButtonData toggleButton = new PushButtonData(
                "ID_TOGGLE_MCP",
                "Connect\r\nServer",
                Assembly.GetExecutingAssembly().Location,
                "revit_mcp_plugin.Core.MCPServiceConnection");
            toggleButton.ToolTip = "Start or stop MCP server connection";
            toggleButton.LargeImage = RibbonIconHelper.GetLargeImage("mcp");
            toggleButton.Image = RibbonIconHelper.GetSmallImage("mcp");
            mcpPanel.AddItem(toggleButton);

            PushButtonData settingsButton = new PushButtonData(
                "ID_MCP_SETTINGS",
                "Settings",
                Assembly.GetExecutingAssembly().Location,
                "revit_mcp_plugin.Core.Settings");
            settingsButton.ToolTip = "MCP Plugin Settings";
            settingsButton.LargeImage = RibbonIconHelper.GetLargeImage("settings");
            settingsButton.Image = RibbonIconHelper.GetSmallImage("settings");
            mcpPanel.AddItem(settingsButton);

            RibbonPanel toolsPanel = application.CreateRibbonPanel(TabName, PanelNameTools);
            PushButtonData exportSheetsButton = new PushButtonData(
                "ID_EXPORT_SHEETS",
                "Export Sheets\r\nto Excel",
                Assembly.GetExecutingAssembly().Location,
                "revit_mcp_plugin.Core.ExportSheetsToExcel");
            exportSheetsButton.ToolTip = "Export all sheets with selected properties to Excel";
            exportSheetsButton.LargeImage = RibbonIconHelper.GetLargeImage("export");
            exportSheetsButton.Image = RibbonIconHelper.GetSmallImage("export");
            toolsPanel.AddItem(exportSheetsButton);

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
