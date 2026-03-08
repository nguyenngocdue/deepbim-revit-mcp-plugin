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

            string toolsDllPath = Path.Combine(Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location), "CommandDeepBimMCPTools.dll");
            if (File.Exists(toolsDllPath))
            {
                AddTestCommand(toolsPanel, toolsDllPath, "Test Say Hello", "DeepBimMCPTools.TestSayHelloCommand", "ID_TEST_SAY_HELLO", "test_hello");
                AddTestCommand(toolsPanel, toolsDllPath, "Test View Info", "DeepBimMCPTools.TestGetCurrentViewInfoCommand", "ID_TEST_VIEW_INFO", "test_view");
                AddTestCommand(toolsPanel, toolsDllPath, "Test Room Data", "DeepBimMCPTools.TestExportRoomDataCommand", "ID_TEST_ROOM_DATA", "test_room");
                AddTestCommand(toolsPanel, toolsDllPath, "Test Sheet Props", "DeepBimMCPTools.TestGetSheetPropertiesCommand", "ID_TEST_SHEET_PROPS", "test_sheet");
                AddTestCommand(toolsPanel, toolsDllPath, "Test Export Sheets", "DeepBimMCPTools.TestExportSheetsCommand", "ID_TEST_EXPORT_SHEETS", "export");
            }

            return Result.Succeeded;
        }

        private static void AddTestCommand(RibbonPanel panel, string assemblyPath, string buttonText, string className, string id, string iconKind = "mcp")
        {
            var push = new PushButtonData(id, buttonText, assemblyPath, className);
            push.ToolTip = "Test: " + buttonText + " (chạy trực tiếp trong Revit, không cần server)";
            push.LargeImage = RibbonIconHelper.GetLargeImage(iconKind);
            push.Image = RibbonIconHelper.GetSmallImage(iconKind);
            panel.AddItem(push);
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
