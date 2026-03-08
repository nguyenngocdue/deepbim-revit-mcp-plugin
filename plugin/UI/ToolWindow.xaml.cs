using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shell;

namespace revit_mcp_plugin.UI
{
    /// <summary>
    /// Cửa sổ dùng chung: chỉ cần set WindowTitle và Content (UserControl/Page).
    /// </summary>
    public partial class ToolWindow : Window
    {
        public static readonly System.Windows.DependencyProperty WindowTitleProperty =
            System.Windows.DependencyProperty.Register(nameof(WindowTitle), typeof(string), typeof(ToolWindow),
                new PropertyMetadata("DeepBim-MCP", (d, e) => { if (d is Window w && e.NewValue is string t) w.Title = t; }));

        public string WindowTitle
        {
            get => (string)GetValue(WindowTitleProperty);
            set => SetValue(WindowTitleProperty, value);
        }

        public ToolWindow()
        {
            InitializeComponent();
            var chrome = new WindowChrome
            {
                CornerRadius = new CornerRadius(10),
                CaptionHeight = 44,
                ResizeBorderThickness = new Thickness(6)
            };
            WindowChrome.SetWindowChrome(this, chrome);
            Loaded += OnLoaded;
            RootBorder.SizeChanged += (s, _) => UpdateClip();
        }

        /// <summary>Gán nội dung hiển thị (Page, UserControl, hoặc bất kỳ UIElement nào).</summary>
        public void SetContent(object content)
        {
            ContentHost.Content = content;
        }

        private void UpdateClip()
        {
            if (RootBorder.ActualWidth <= 0 || RootBorder.ActualHeight <= 0) return;
            RootBorder.Clip = new RectangleGeometry(
                new Rect(0, 0, RootBorder.ActualWidth, RootBorder.ActualHeight), 10, 10);
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            UpdateClip();
            try
            {
                var uri = new System.Uri("pack://application:,,,/RevitMCPPlugin;component/Resources/icon.png", System.UriKind.Absolute);
                TitleBarIcon.Source = BitmapFrame.Create(uri);
                Icon = BitmapFrame.Create(uri);
            }
            catch { /* icon optional */ }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
