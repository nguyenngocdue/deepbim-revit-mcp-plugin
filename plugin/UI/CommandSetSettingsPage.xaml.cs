using Newtonsoft.Json;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace revit_mcp_plugin.UI
{
    public partial class CommandSetSettingsPage : Page
    {
        private ObservableCollection<CommandSetInfo> commandSets;
        private ObservableCollection<CommandConfig> currentCommands;

        public CommandSetSettingsPage()
        {
            InitializeComponent();

            commandSets = new ObservableCollection<CommandSetInfo>();
            currentCommands = new ObservableCollection<CommandConfig>();

            CommandSetListBox.ItemsSource = commandSets;
            FeaturesListView.ItemsSource = currentCommands;

            FeaturesListView.Loaded += (s, _) => UpdateDescriptionColumnWidth();
            FeaturesListView.SizeChanged += (s, _) => UpdateDescriptionColumnWidth();

            LoadCommandSets();
            if (commandSets.Count > 0)
                CommandSetListBox.SelectedIndex = 0;
            NoSelectionTextBlock.Visibility = commandSets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private const double OnColumnWidth = 44;
        private const double CommandColumnWidth = 180;
        private void UpdateDescriptionColumnWidth()
        {
            if (FeaturesListView.ActualWidth <= 0) return;
            double remaining = FeaturesListView.ActualWidth - OnColumnWidth - CommandColumnWidth - 24; // 24 = scrollbar/margin
            DescriptionColumn.Width = Math.Max(200, remaining);
        }

        private void UpdateEnabledCount()
        {
            if (currentCommands == null || currentCommands.Count == 0)
            {
                EnabledCountTextBlock.Text = "";
                return;
            }
            int n = currentCommands.Count(c => c.Enabled);
            EnabledCountTextBlock.Text = n == currentCommands.Count ? "All enabled" : $"{n} enabled";
        }

        private void ApplySearchFilter()
        {
            var view = CollectionViewSource.GetDefaultView(FeaturesListView.ItemsSource);
            if (view == null) return;
            string q = SearchBox?.Text?.Trim().ToUpperInvariant() ?? "";
            view.Filter = string.IsNullOrEmpty(q)
                ? null
                : (Predicate<object>)(obj => obj is CommandConfig c &&
                    ((c.CommandName ?? "").ToUpperInvariant().Contains(q) ||
                     (c.Description ?? "").ToUpperInvariant().Contains(q)));
        }

        private void SearchBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplySearchFilter();
        }

        private void LoadCommandSets()
        {
            try
            {
                commandSets.Clear();
                string[] candidates = PathManager.GetCandidateCommandsPaths();

                foreach (string commandsDirectory in candidates)
                {
                    if (string.IsNullOrEmpty(commandsDirectory) || !Directory.Exists(commandsDirectory))
                        continue;

                    var availableCommandSets = new Dictionary<string, CommandSetInfo>();
                    var availableCommandNames = new HashSet<string>();
                    string registryFilePath = Path.Combine(commandsDirectory, "commandRegistry.json");

                    foreach (var directory in Directory.GetDirectories(commandsDirectory))
                    {
                        if (Path.GetFileName(directory).StartsWith("."))
                            continue;

                        string commandJsonPath = Path.Combine(directory, "command.json");
                        if (!File.Exists(commandJsonPath))
                            continue;

                        string commandJson = File.ReadAllText(commandJsonPath);
                        var commandSetData = JsonConvert.DeserializeObject<CommandJsonModel>(commandJson);
                        if (commandSetData == null)
                            continue;

                        var newCommandSet = new CommandSetInfo
                        {
                            Name = commandSetData.Name,
                            Description = commandSetData.Description,
                            Commands = new List<CommandConfig>()
                        };

                        var versionDirectories = Directory.GetDirectories(directory)
                            .Select(Path.GetFileName)
                            .Where(name => int.TryParse(name, out _))
                            .ToList();

                        foreach (var command in commandSetData.Commands)
                        {
                            string dllBasePath = null;
                            var supportedVersions = new List<string>();

                            foreach (var version in versionDirectories)
                            {
                                string versionDir = Path.Combine(directory, version);
                                if (!string.IsNullOrEmpty(command.AssemblyPath))
                                {
                                    string dllFileName = Path.GetFileName(command.AssemblyPath.Replace("{VERSION}", version));
                                    if (File.Exists(Path.Combine(versionDir, dllFileName)))
                                    {
                                        dllBasePath ??= Path.Combine(commandSetData.Name, "{VERSION}", dllFileName);
                                        supportedVersions.Add(version);
                                    }
                                }
                                else
                                {
                                    var dlls = Directory.GetFiles(versionDir, "*.dll");
                                    if (dlls.Length > 0)
                                    {
                                        dllBasePath ??= Path.Combine(commandSetData.Name, "{VERSION}", Path.GetFileName(dlls[0]));
                                        supportedVersions.Add(version);
                                    }
                                }
                            }

                            if (supportedVersions.Count > 0 && dllBasePath != null)
                            {
                                newCommandSet.Commands.Add(new CommandConfig
                                {
                                    CommandName = command.CommandName,
                                    Description = command.Description,
                                    AssemblyPath = dllBasePath,
                                    Enabled = false,
                                    SupportedRevitVersions = supportedVersions.ToArray()
                                });
                                availableCommandNames.Add(command.CommandName);
                            }
                        }

                        if (newCommandSet.Commands.Any())
                            availableCommandSets[commandSetData.Name] = newCommandSet;
                    }

                    if (availableCommandSets.Count == 0)
                        continue;

                    if (File.Exists(registryFilePath))
                    {
                        try
                        {
                            var registry = JsonConvert.DeserializeObject<CommandRegistryModel>(File.ReadAllText(registryFilePath));
                            if (registry?.Commands != null)
                                foreach (var item in registry.Commands)
                                    if (availableCommandNames.Contains(item.CommandName))
                                        foreach (var cs in availableCommandSets.Values)
                                        {
                                            var cmd = cs.Commands.FirstOrDefault(c => c.CommandName == item.CommandName);
                                            if (cmd != null) cmd.Enabled = item.Enabled;
                                        }
                        }
                        catch { }
                    }

                    foreach (var cs in availableCommandSets.Values)
                        commandSets.Add(cs);

                    PathManager.SetPluginDirectory(Path.GetDirectoryName(commandsDirectory));
                    break;
                }

                if (commandSets.Count == 0)
                {
                    string msg = "No command sets found.\n\n";
                    msg += "• Dev: Build full solution (Plugin + RevitMCPCommandSet), then load add-in from Add-in Manager → plugin\\bin\\AddIn <version> Debug (version from RevitVersions.json).\n";
                    msg += "• Deploy: Run setup-revit-addin.ps1 to copy to AppData.\n\n";
                    msg += "Check: Commands\\RevitMCPCommandSet\\command.json and Commands\\RevitMCPCommandSet\\<version>\\RevitMCPCommandSet.dll.";
                    MessageBox.Show(msg, "No Command Sets", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading command sets: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CommandSetListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UnsubscribeCommandConfigEvents();
            currentCommands.Clear();
            var selected = CommandSetListBox.SelectedItem as CommandSetInfo;
            if (selected != null)
            {
                NoSelectionTextBlock.Visibility = Visibility.Collapsed;
                FeaturesHeaderTextBlock.Text = $"{selected.Name}";
                foreach (var cmd in selected.Commands)
                {
                    currentCommands.Add(cmd);
                    cmd.PropertyChanged += CommandConfig_PropertyChanged;
                }
                ApplySearchFilter();
                UpdateEnabledCount();
            }
            else
            {
                NoSelectionTextBlock.Visibility = Visibility.Visible;
                FeaturesHeaderTextBlock.Text = "Commands";
                EnabledCountTextBlock.Text = "";
            }
        }

        private void UnsubscribeCommandConfigEvents()
        {
            foreach (var cmd in currentCommands.OfType<CommandConfig>())
                cmd.PropertyChanged -= CommandConfig_PropertyChanged;
        }

        private void CommandConfig_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(CommandConfig.Enabled))
                UpdateEnabledCount();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            var idx = CommandSetListBox.SelectedIndex;
            LoadCommandSets();
            if (idx >= 0 && idx < commandSets.Count)
                CommandSetListBox.SelectedIndex = idx;
        }

        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cmd in currentCommands) cmd.Enabled = true;
            FeaturesListView.Items.Refresh();
            UpdateEnabledCount();
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cmd in currentCommands) cmd.Enabled = false;
            FeaturesListView.Items.Refresh();
            UpdateEnabledCount();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string registryPath = PathManager.GetCommandRegistryFilePath();

                var existingDict = new Dictionary<string, CommandConfig>();
                if (File.Exists(registryPath))
                {
                    string json = File.ReadAllText(registryPath);
                    var existing = JsonConvert.DeserializeObject<CommandRegistryModel>(json);
                    if (existing?.Commands != null)
                    {
                        foreach (var cmd in existing.Commands)
                            existingDict[cmd.CommandName] = cmd;
                    }
                }

                var registry = new CommandRegistryModel { Commands = new List<CommandConfig>() };

                foreach (var cs in commandSets)
                {
                    foreach (var cmd in cs.Commands)
                    {
                        if (!cmd.Enabled) continue;

                        if (existingDict.TryGetValue(cmd.CommandName, out var existingCmd))
                        {
                            existingCmd.Enabled = true;
                            existingCmd.AssemblyPath = cmd.AssemblyPath;
                            existingCmd.SupportedRevitVersions = cmd.SupportedRevitVersions;
                            registry.Commands.Add(existingCmd);
                        }
                        else
                        {
                            registry.Commands.Add(new CommandConfig
                            {
                                CommandName = cmd.CommandName,
                                AssemblyPath = cmd.AssemblyPath ?? "",
                                Enabled = true,
                                Description = cmd.Description,
                                SupportedRevitVersions = cmd.SupportedRevitVersions
                            });
                        }
                    }
                }

                int count = registry.Commands.Count;
                string summary = string.Join("\n",
                    registry.Commands.Select(c => $"  • {c.CommandName}"));

                File.WriteAllText(registryPath,
                    JsonConvert.SerializeObject(registry, Formatting.Indented));

                MessageBox.Show(
                    $"Settings saved!\n\nEnabled {count} command(s):\n{summary}",
                    "Saved", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start("explorer.exe", PathManager.GetCommandsDirectoryPath());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening folder: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public class CommandSetInfo
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
    }

    public class CommandRegistryModel
    {
        [JsonProperty("commands")]
        public List<CommandConfig> Commands { get; set; } = new List<CommandConfig>();
    }

    public class CommandJsonModel
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("commands")]
        public List<CommandItemModel> Commands { get; set; } = new List<CommandItemModel>();

        [JsonProperty("developer")]
        public DeveloperInfo Developer { get; set; }
    }

    public class CommandItemModel
    {
        [JsonProperty("commandName")]
        public string CommandName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("assemblyPath")]
        public string AssemblyPath { get; set; }
    }
}
