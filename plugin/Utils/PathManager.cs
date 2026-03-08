using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Utils
{
    public static class PathManager
    {
        private static string _pluginDirectory;

        public static string GetPluginDirectoryPath()
        {
            if (_pluginDirectory != null)
                return _pluginDirectory;

            // Method 1: Revit Addins path (where the add-in is actually deployed - always writable)
            string addinsBase = GetRevitAddinsPluginPath();
            if (!string.IsNullOrEmpty(addinsBase))
            {
                _pluginDirectory = addinsBase;
                return _pluginDirectory;
            }

            // Method 2: Assembly.Location (only if writable - avoid Program Files / dotnet runtime)
            string location = typeof(PathManager).Assembly.Location;
            if (!string.IsNullOrEmpty(location))
            {
                string dir = Path.GetDirectoryName(location);
                if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && IsWritablePluginPath(dir))
                {
                    _pluginDirectory = dir;
                    return _pluginDirectory;
                }
            }

            // Method 3: Search loaded assemblies for RevitMCPPlugin (only if writable)
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (asm.GetName().Name == "RevitMCPPlugin" && !string.IsNullOrEmpty(asm.Location))
                    {
                        string dir = Path.GetDirectoryName(asm.Location);
                        if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir) && IsWritablePluginPath(dir))
                        {
                            _pluginDirectory = dir;
                            return _pluginDirectory;
                        }
                    }
                }
                catch { }
            }

            // Method 4: Fallback to %APPDATA%\DeepBim-MCP (guaranteed writable)
            _pluginDirectory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeepBim-MCP");
            if (!Directory.Exists(_pluginDirectory))
                Directory.CreateDirectory(_pluginDirectory);

            return _pluginDirectory;
        }

        /// <summary>
        /// Returns true if the path is a writable plugin root (not under Program Files or dotnet runtime).
        /// </summary>
        private static bool IsWritablePluginPath(string dir)
        {
            if (string.IsNullOrEmpty(dir)) return false;
            string full = Path.GetFullPath(dir).ToUpperInvariant();
            if (full.Contains("PROGRAM FILES")) return false;
            if (full.Contains("PROGRAM FILES (X86)")) return false;
            if (full.Contains("DOTNET\\SHARED\\")) return false;
            return true;
        }

        /// <summary>
        /// Gets the add-in folder in Revit Addins (e.g. %APPDATA%\Autodesk\Revit\Addins\2025\revit_mcp_plugin) if it exists.
        /// </summary>
        private static string GetRevitAddinsPluginPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] revitVersions = { "2025", "2024", "2026", "2023" };
            foreach (var ver in revitVersions)
            {
                string pluginDir = Path.Combine(appData, "Autodesk", "Revit", "Addins", ver, "revit_mcp_plugin");
                if (Directory.Exists(pluginDir))
                    return pluginDir;
            }
            return null;
        }

        /// <summary>
        /// Allows setting the plugin directory manually (e.g. from ExternalCommand context)
        /// </summary>
        public static void SetPluginDirectory(string path)
        {
            if (Directory.Exists(path))
                _pluginDirectory = path;
        }

        public static string GetAppDataDirectoryPath()
        {
            return GetPluginDirectoryPath();
        }

        public static string GetCommandsDirectoryPath()
        {
            string baseDir = GetPluginDirectoryPath();
            string dir = Path.Combine(baseDir, "Commands");
            try
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
            catch (UnauthorizedAccessException)
            {
                // Fallback to writable AppData if plugin dir is not writable (e.g. Program Files)
                dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DeepBim-MCP", "Commands");
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                return dir;
            }
        }

        public static string GetLogsDirectoryPath()
        {
            // Logs always go to %APPDATA% — guaranteed writable
            string dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DeepBim-MCP", "Logs");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetCommandRegistryFilePath(bool createIfNotExists = true)
        {
            string commandsDir = GetCommandsDirectoryPath();
            string registryPath = Path.Combine(commandsDir, "commandRegistry.json");

            // If primary path has no valid registry, try Revit Addins path (common when Assembly.Location is empty on .NET 8)
            if (!File.Exists(registryPath) || IsRegistryEmpty(registryPath))
            {
                string fallbackPath = TryGetRevitAddinsCommandsPath();
                if (!string.IsNullOrEmpty(fallbackPath))
                {
                    registryPath = Path.Combine(fallbackPath, "commandRegistry.json");
                    if (File.Exists(registryPath))
                    {
                        string pluginDir = Path.GetDirectoryName(fallbackPath);
                        if (Directory.Exists(pluginDir))
                            _pluginDirectory = pluginDir;
                        return registryPath;
                    }
                }
            }

            if (createIfNotExists && !File.Exists(registryPath))
            {
                CreateDefaultCommandRegistryFile(registryPath);
            }

            return registryPath;
        }

        private static bool IsRegistryEmpty(string path)
        {
            try
            {
                string json = File.ReadAllText(path);
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                var commands = obj?["commands"] as Newtonsoft.Json.Linq.JArray;
                return commands == null || commands.Count == 0;
            }
            catch { return true; }
        }

        /// <summary>
        /// Tries to find Commands folder in Revit Addins path (used when primary path is empty/wrong).
        /// </summary>
        public static string TryGetRevitAddinsCommandsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] revitVersions = { "2025", "2024", "2026", "2023" };
            foreach (var ver in revitVersions)
            {
                string commandsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins", ver, "revit_mcp_plugin", "Commands");
                string registryFile = Path.Combine(commandsDir, "commandRegistry.json");
                if (File.Exists(registryFile) && !IsRegistryEmpty(registryFile))
                    return commandsDir;
            }
            return null;
        }

        private static void CreateDefaultCommandRegistryFile(string filePath)
        {
            try
            {
                var defaultRegistry = new { commands = new object[] { } };
                string json = JsonConvert.SerializeObject(defaultRegistry, Formatting.Indented);
                File.WriteAllText(filePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error creating default registry: {ex.Message}");
            }
        }
    }
}
