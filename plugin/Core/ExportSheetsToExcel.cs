using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using revit_mcp_plugin.UI;
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Interop;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class ExportSheetsToExcel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                var uiApp = commandData?.Application;
                if (uiApp?.ActiveUIDocument?.Document == null)
                {
                    TaskDialog.Show("Export Sheets", "Open a document first.");
                    return Result.Failed;
                }

                var window = new ToolWindow
                {
                    WindowTitle = "Export Sheets to Excel",
                    Height = 520,
                    Width = 480
                };
                window.SetContent(new ExportSheetsView(uiApp));
                try
                {
                    var helper = new WindowInteropHelper(window)
                    {
                        Owner = Process.GetCurrentProcess().MainWindowHandle
                    };
                }
                catch { }
                window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                window.Show();
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                TaskDialog.Show("Export Sheets", $"Error: {ex.Message}\n\n{ex.StackTrace}");
                return Result.Failed;
            }
        }
    }
}
