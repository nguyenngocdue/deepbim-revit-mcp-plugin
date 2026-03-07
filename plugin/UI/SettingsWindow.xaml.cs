using System.Windows;
using System.Windows.Controls;

namespace revit_mcp_plugin.UI
{
    public partial class SettingsWindow : Window
    {
        private CommandSetSettingsPage commandSetPage;
        private bool isInitialized = false;

        public SettingsWindow()
        {
            InitializeComponent();

            commandSetPage = new CommandSetSettingsPage();
            ContentFrame.Navigate(commandSetPage);

            isInitialized = true;
        }

        private void NavListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!isInitialized) return;

            if (NavListBox.SelectedItem == CommandSetItem)
            {
                ContentFrame.Navigate(commandSetPage);
            }
        }
    }
}
