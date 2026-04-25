using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace revit_mcp_plugin.UI
{
    internal static class BrandingAssets
    {
        private const string ResourceRoot = "pack://application:,,,/RevitMCPPlugin;component/Resources/";

        private static readonly Uri WindowIconUri = new Uri(ResourceRoot + "deepbim-logo-512.png", UriKind.Absolute);
        private static readonly Uri TitleBarLogoUri = new Uri(ResourceRoot + "deepbim-logo-56.png", UriKind.Absolute);

        public static void Apply(Window window, Image titleBarIcon)
        {
            try
            {
                window.Icon = BitmapFrame.Create(WindowIconUri);
                titleBarIcon.Source = BitmapFrame.Create(TitleBarLogoUri);
            }
            catch
            {
                // Branding should never block the Revit add-in UI from opening.
            }
        }
    }
}
