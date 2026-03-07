using Newtonsoft.Json;
using RevitMCPSDK.API.Interfaces;
using revit_mcp_plugin.Utils;
using System;
using System.IO;

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
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    Config = JsonConvert.DeserializeObject<FrameworkConfig>(json);
                    _logger.Info("Configuration loaded: {0} ({1} commands)", _configPath, Config?.Commands?.Count ?? 0);
                    if (Config?.Commands == null || Config.Commands.Count == 0)
                        _logger.Warning("No commands in registry - say_hello will fail. Check Commands folder and commandRegistry.json.");
                }
                else
                {
                    Config = new FrameworkConfig();
                    _logger.Warning("No configuration file found at {0}, using defaults.", _configPath);
                }
            }
            catch (Exception ex)
            {
                Config = new FrameworkConfig();
                _logger.Error("Failed to load configuration: {0}", ex.Message);
            }
        }
    }
}
