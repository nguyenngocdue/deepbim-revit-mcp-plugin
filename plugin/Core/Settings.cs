using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using revit_mcp_plugin.UI;
using System;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class Settings : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var window = new ToolWindow
                {
                    WindowTitle = "DeepBim-MCP Settings",
                    Height = 577,
                    Width = 1010
                };
                window.SetContent(new CommandSetSettingsPage());
                var helper = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle
                };
                window.WindowStartupLocation = System.Windows.WindowStartupLocation.CenterScreen;
                window.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("DeepBim-MCP Settings", $"Error: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
