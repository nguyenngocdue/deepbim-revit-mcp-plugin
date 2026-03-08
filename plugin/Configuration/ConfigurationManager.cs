using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Configuration
{
    public class ConfigurationManager
    {
        private readonly ILogger _logger;
        private readonly string _configPath;

        public FrameworkConfig Config { get; private set; }

        public ConfigurationManager(ILogger logger)
        {
            _logger = logger;
            _configPath = PathManager.GetCommandRegistryFilePath();
        }

        public void LoadConfiguration()
        {
            try
            {
                Config = new FrameworkConfig { Commands = new List<CommandConfig>() };

                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var loaded = JsonConvert.DeserializeObject<FrameworkConfig>(json);
                    if (loaded?.Commands != null && loaded.Commands.Count > 0)
                    {
                        Config.Commands = loaded.Commands;
                        _logger.Info("Configuration loaded: {0} ({1} commands)", _configPath, Config.Commands.Count);
                    }
                }
                else
                {
                    _logger.Warning("No configuration file at {0}, will try command set.", _configPath);
                }

                if (Config.Commands == null || Config.Commands.Count == 0)
                    LoadFromCommandSetFallback();
                else
                    MergeFromCommandSet();

                if (Config.Commands == null || Config.Commands.Count == 0)
                    _logger.Warning("No commands loaded - say_hello will fail. Run setup-revit-addin.ps1 and restart Revit.");
            }
            catch (Exception ex)
            {
                Config = new FrameworkConfig { Commands = new List<CommandConfig>() };
                _logger.Error("Failed to load configuration: {0}", ex.Message);
            }
        }

        private void LoadFromCommandSetFallback()
        {
            string commandsDir = PathManager.GetCommandsDirectoryPath();
            string path = Path.Combine(commandsDir, "RevitMCPCommandSet", "command.json");
            try
            {
                if (!File.Exists(path)) return;
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                var arr = data?["commands"] as Newtonsoft.Json.Linq.JArray;
                if (arr == null) return;
                var list = new List<CommandConfig>();
                foreach (var token in arr)
                {
                    var cmd = token as Newtonsoft.Json.Linq.JObject;
                    if (cmd == null) continue;
                    string name = cmd["commandName"]?.ToString();
                    string assemblyPath = cmd["assemblyPath"]?.ToString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(assemblyPath)) continue;
                    list.Add(new CommandConfig
                    {
                        CommandName = name,
                        Description = cmd["description"]?.ToString() ?? "",
                        AssemblyPath = assemblyPath,
                        Enabled = true
                    });
                }
                if (list.Count > 0)
                {
                    Config.Commands = list;
                    _logger.Info("Loaded {0} commands from command set (fallback).", list.Count);
                    try
                    {
                        File.WriteAllText(_configPath, JsonConvert.SerializeObject(new { commands = list }, Formatting.Indented));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { _logger.Error("Command set fallback: {0}", ex.Message); }
        }

        private void MergeFromCommandSet()
        {
            string commandsDir = PathManager.GetCommandsDirectoryPath();
            string path = Path.Combine(commandsDir, "RevitMCPCommandSet", "command.json");
            try
            {
                if (!File.Exists(path)) return;
                var existing = new HashSet<string>(Config.Commands.Select(c => c.CommandName), StringComparer.OrdinalIgnoreCase);
                string json = File.ReadAllText(path);
                var data = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                var arr = data?["commands"] as Newtonsoft.Json.Linq.JArray;
                if (arr == null) return;
                int added = 0;
                foreach (var token in arr)
                {
                    var cmd = token as Newtonsoft.Json.Linq.JObject;
                    if (cmd == null) continue;
                    string name = cmd["commandName"]?.ToString();
                    string assemblyPath = cmd["assemblyPath"]?.ToString();
                    if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(assemblyPath) || existing.Contains(name)) continue;
                    Config.Commands.Add(new CommandConfig
                    {
                        CommandName = name,
                        Description = cmd["description"]?.ToString() ?? "",
                        AssemblyPath = assemblyPath,
                        Enabled = true
                    });
                    existing.Add(name);
                    added++;
                }
                if (added > 0)
                {
                    _logger.Info("Merged {0} new command(s). Total: {1}", added, Config.Commands.Count);
                    try
                    {
                        File.WriteAllText(_configPath, JsonConvert.SerializeObject(new { commands = Config.Commands }, Formatting.Indented));
                    }
                    catch { }
                }
            }
            catch (Exception ex) { _logger.Error("MergeFromCommandSet: {0}", ex.Message); }
        }
    }
}
