using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using revit_mcp_plugin.UI;
using revit_mcp_plugin.Utils;
using System;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Core
{
    [Transaction(TransactionMode.Manual)]
    public class MCPServiceConnection : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                ResolvePluginDirectory();

                var window = new MCPStatusWindow(commandData.Application);
                _ = new System.Windows.Interop.WindowInteropHelper(window)
                {
                    Owner = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle
                };
                window.Show();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("DeepBim-MCP",
                    $"Failed to open control panel:\n\n{ex.Message}\n\n{ex.StackTrace}");
                message = ex.Message;
                return Result.Failed;
            }
        }

        private void ResolvePluginDirectory()
        {
            var pluginAsm = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => a.GetName().Name == "RevitMCPPlugin");

            if (pluginAsm != null)
            {
                string location = pluginAsm.Location;
                if (!string.IsNullOrEmpty(location))
                {
                    string dir = Path.GetDirectoryName(location);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        PathManager.SetPluginDirectory(dir);
                        return;
                    }
                }

                try
                {
                    #pragma warning disable SYSLIB0012
                    string codeBase = pluginAsm.CodeBase;
                    #pragma warning restore SYSLIB0012
                    if (!string.IsNullOrEmpty(codeBase))
                    {
                        var uri = new Uri(codeBase);
                        string dir = Path.GetDirectoryName(uri.LocalPath);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                        {
                            PathManager.SetPluginDirectory(dir);
                            return;
                        }
                    }
                }
                catch { }
            }
        }
    }
}
