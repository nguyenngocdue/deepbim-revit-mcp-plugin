using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace revit_mcp_plugin.Core
{
    /// <summary>
    /// Creates ribbon button images (32x32 large, 16x16 small) using Segoe MDL2 Assets.
    /// Connect Server = power (On/Off); Settings = gear. DeepBIM accent #59DCCB.
    /// </summary>
    public static class RibbonIconHelper
    {
        private static readonly Color AccentColor = Color.FromRgb(0x59, 0xDC, 0xCB);
        private static readonly Color AccentDark = Color.FromRgb(0x2F, 0xB3, 0xA5);

        private const string SegoeMdl2 = "Segoe MDL2 Assets";

        // Segoe MDL2 Assets: Power = On/Off, Setting = gear
        private const string GlyphConnect = "\uE7E8"; // Power (On/Off)
        private const string GlyphSettings = "\uE713"; // Setting

        public static BitmapSource GetLargeImage(string kind = "mcp")
        {
            return CreateIcon(32, kind);
        }

        public static BitmapSource GetSmallImage(string kind = "mcp")
        {
            return CreateIcon(16, kind);
        }

        private static BitmapSource CreateIcon(int size, string kind)
        {
            string glyph = kind == "settings" ? GlyphSettings : GlyphConnect;
            var dv = new DrawingVisual();
            using (var dc = dv.RenderOpen())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, size, size));

                var typeface = new Typeface(
                    new FontFamily(SegoeMdl2),
                    FontStyles.Normal,
                    FontWeights.Normal,
                    FontStretches.Normal);

                double fontSize = size * 0.65;
                const double pixelsPerDip = 1.0;

                var formattedText = new FormattedText(
                    glyph,
                    System.Globalization.CultureInfo.CurrentUICulture,
                    FlowDirection.LeftToRight,
                    typeface,
                    fontSize,
                    Brushes.White,
                    null,
                    pixelsPerDip);

                double x = (size - formattedText.Width) / 2;
                double y = (size - formattedText.Height) / 2;

                var bgRect = new Rect(0, 0, size, size);
                var bgBrush = new SolidColorBrush(AccentColor);
                var borderBrush = new SolidColorBrush(AccentDark);
                dc.DrawRoundedRectangle(bgBrush, new Pen(borderBrush, size > 20 ? 1 : 0.5), bgRect, size * 0.2, size * 0.2);
                dc.DrawText(formattedText, new Point(x, y));
            }

            var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
            bmp.Render(dv);
            bmp.Freeze();
            return bmp;
        }
    }
}
