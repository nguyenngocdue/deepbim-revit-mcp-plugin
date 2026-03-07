using Autodesk.Revit.UI;
using revit_mcp_plugin.Core;
using revit_mcp_plugin.Utils;
using System;
using System.Windows;
using System.Windows.Media;

namespace revit_mcp_plugin.UI
{
    public partial class MCPStatusWindow : Window
    {
        private readonly UIApplication _uiApp;

        private static readonly SolidColorBrush GreenBrush = new SolidColorBrush(Color.FromRgb(0x2E, 0xCC, 0x40));
        private static readonly SolidColorBrush RedBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0x33, 0x33));
        private static readonly SolidColorBrush GrayBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

        public MCPStatusWindow(UIApplication uiApp)
        {
            InitializeComponent();
            _uiApp = uiApp;

            PluginDirText.Text = PathManager.GetPluginDirectoryPath();
            AppendLog($"Plugin: {PathManager.GetPluginDirectoryPath()}");
            AppendLog($"Commands: {PathManager.GetCommandsDirectoryPath()}");
            RefreshStatus();

            // Auto-start when window opens if server is not running (easier for Claude connection)
            if (!SocketService.Instance.IsRunning)
            {
                Loaded += (s, e) =>
                {
                    if (!SocketService.Instance.IsRunning)
                        StartButton_Click(StartButton, e);
                };
            }
        }

        private void RefreshStatus()
        {
            bool running = SocketService.Instance.IsRunning;
            int port = SocketService.Instance.Port;

            PortText.Text = running ? port.ToString() : "—";

            if (running)
            {
                StatusIndicator.Fill = GreenBrush;
                StatusText.Text = "Running";
                StatusText.Foreground = GreenBrush;
                StartButton.IsEnabled = false;
                StartButton.Opacity = 0.4;
                StopButton.IsEnabled = true;
                StopButton.Opacity = 1.0;
            }
            else
            {
                StatusIndicator.Fill = GrayBrush;
                StatusText.Text = "Stopped";
                StatusText.Foreground = RedBrush;
                StartButton.IsEnabled = true;
                StartButton.Opacity = 1.0;
                StopButton.IsEnabled = false;
                StopButton.Opacity = 0.4;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var service = SocketService.Instance;

                AppendLog("Initializing...");
                service.Initialize(_uiApp);
                AppendLog("Initialized.");

                AppendLog("Finding available port (8080-8099)...");
                service.Start();

                if (service.IsRunning)
                {
                    AppendLog($">>> DeepBim-MCP Server is RUNNING on port {service.Port} <<<");
                }
                else
                {
                    AppendLog("ERROR: Server failed to start.");
                }

                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR: {ex.Message}");
                RefreshStatus();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SocketService.Instance.Stop();
                AppendLog("DeepBim-MCP Server stopped.");
                RefreshStatus();
            }
            catch (Exception ex)
            {
                AppendLog($"ERROR stopping: {ex.Message}");
                RefreshStatus();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void AppendLog(string message)
        {
            string timestamp = DateTime.Now.ToString("HH:mm:ss");
            LogText.Text += $"[{timestamp}] {message}\n";
            LogScroller.ScrollToEnd();
        }
    }
}
