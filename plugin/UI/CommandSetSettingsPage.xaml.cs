using Newtonsoft.Json;
using revit_mcp_plugin.Configuration;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

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

            LoadCommandSets();
            NoSelectionTextBlock.Visibility = Visibility.Visible;
        }

        private void LoadCommandSets()
        {
            try
            {
                commandSets.Clear();
                string commandsDirectory = PathManager.GetCommandsDirectoryPath();
                string registryFilePath = PathManager.GetCommandRegistryFilePath();

                // If primary path has no command set folders, try Revit Addins path (e.g. when Assembly.Location is empty)
                if (!Directory.Exists(commandsDirectory) || !Directory.GetDirectories(commandsDirectory).Any(d => !Path.GetFileName(d).StartsWith(".")))
                {
                    string fallback = PathManager.TryGetRevitAddinsCommandsPath();
                    if (!string.IsNullOrEmpty(fallback))
                    {
                        commandsDirectory = fallback;
                        registryFilePath = Path.Combine(fallback, "commandRegistry.json");
                        PathManager.SetPluginDirectory(Path.GetDirectoryName(fallback));
                    }
                }

                var availableCommandSets = new Dictionary<string, CommandSetInfo>();
                var availableCommandNames = new HashSet<string>();

                string[] commandSetDirectories = Directory.GetDirectories(commandsDirectory);
                foreach (var directory in commandSetDirectories)
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
                                // assemblyPath may be "RevitMCPCommandSet.dll" or "RevitMCPCommandSet/{VERSION}/RevitMCPCommandSet.dll"
                                string dllFileName = Path.GetFileName(command.AssemblyPath.Replace("{VERSION}", version));
                                string versionDllPath = Path.Combine(versionDir, dllFileName);
                                if (File.Exists(versionDllPath))
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
                    {
                        availableCommandSets[commandSetData.Name] = newCommandSet;
                    }
                }

                if (File.Exists(registryFilePath))
                {
                    string registryJson = File.ReadAllText(registryFilePath);
                    var registry = JsonConvert.DeserializeObject<CommandRegistryModel>(registryJson);
                    if (registry?.Commands != null)
                    {
                        var validCommands = new List<CommandConfig>();
                        foreach (var item in registry.Commands)
                        {
                            if (availableCommandNames.Contains(item.CommandName))
                            {
                                validCommands.Add(item);
                                foreach (var cs in availableCommandSets.Values)
                                {
                                    var cmd = cs.Commands.FirstOrDefault(c => c.CommandName == item.CommandName);
                                    if (cmd != null) cmd.Enabled = item.Enabled;
                                }
                            }
                        }

                        if (validCommands.Count != registry.Commands.Count)
                        {
                            registry.Commands = validCommands;
                            File.WriteAllText(registryFilePath,
                                JsonConvert.SerializeObject(registry, Formatting.Indented));
                        }
                    }
                }

                foreach (var cs in availableCommandSets.Values)
                    commandSets.Add(cs);

                if (commandSets.Count == 0)
                {
                    MessageBox.Show(
                        "No command sets found.\nCheck the Commands folder for valid command sets.",
                        "No Command Sets", MessageBoxButton.OK, MessageBoxImage.Information);
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
            currentCommands.Clear();
            var selected = CommandSetListBox.SelectedItem as CommandSetInfo;
            if (selected != null)
            {
                NoSelectionTextBlock.Visibility = Visibility.Collapsed;
                FeaturesHeaderTextBlock.Text = $"{selected.Name} - Commands";
                foreach (var cmd in selected.Commands)
                    currentCommands.Add(cmd);
            }
            else
            {
                NoSelectionTextBlock.Visibility = Visibility.Visible;
                FeaturesHeaderTextBlock.Text = "Command List";
            }
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
        }

        private void UnselectAllButton_Click(object sender, RoutedEventArgs e)
        {
            foreach (var cmd in currentCommands) cmd.Enabled = false;
            FeaturesListView.Items.Refresh();
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
