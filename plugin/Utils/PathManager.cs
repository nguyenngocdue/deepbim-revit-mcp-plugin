using Newtonsoft.Json;
using System;
using System.IO;
using System.Linq;

namespace revit_mcp_plugin.Utils
{
    public static class PathManager
    {
        private static string _pluginDirectory;

        private const string EnvFileName = "deepbim-mcp.env.json";

        public static string GetPluginDirectoryPath()
        {
            if (_pluginDirectory != null)
                return _pluginDirectory;

            // Method 0: env file — dev = dùng thư mục chứa DLL (build output), deploy = dùng AppData
            string assemblyDir = GetAssemblyDirectory();
            if (!string.IsNullOrEmpty(assemblyDir))
            {
                string mode = TryReadEnvMode(assemblyDir);
                if (string.Equals(mode, "dev", StringComparison.OrdinalIgnoreCase) && IsWritablePluginPath(assemblyDir))
                {
                    _pluginDirectory = assemblyDir;
                    return _pluginDirectory;
                }
            }

            // Method 1: Deploy — Revit Addins path (AppData)
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

        private static string GetAssemblyDirectory()
        {
            // Ưu tiên thư mục đã set lúc startup (Application.OnStartup) khi Location trống hoặc trỏ vào temp
            if (!string.IsNullOrEmpty(_pluginDirectory) && Directory.Exists(_pluginDirectory))
                return _pluginDirectory;
            try
            {
                string loc = typeof(PathManager).Assembly.Location;
                if (!string.IsNullOrEmpty(loc))
                {
                    string dir = Path.GetDirectoryName(loc);
                    if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                    {
                        string full = Path.GetFullPath(dir).ToUpperInvariant();
                        if (!full.Contains("\\TEMP\\") && !full.Contains("\\APPDATA\\LOCAL\\TEMP\\"))
                            return dir;
                    }
                }
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        if (asm.GetName().Name == "RevitMCPPlugin" && !string.IsNullOrEmpty(asm.Location))
                        {
                            string dir = Path.GetDirectoryName(asm.Location);
                            if (!string.IsNullOrEmpty(dir) && Directory.Exists(dir))
                            {
                                string full = Path.GetFullPath(dir).ToUpperInvariant();
                                if (!full.Contains("\\TEMP\\") && !full.Contains("\\APPDATA\\LOCAL\\TEMP\\"))
                                    return dir;
                            }
                        }
                    }
                    catch { }
                }
            }
            catch { }
            return null;
        }

        private static string TryReadEnvMode(string pluginDir)
        {
            try
            {
                string path = Path.Combine(pluginDir, EnvFileName);
                if (!File.Exists(path)) return null;
                string json = File.ReadAllText(path);
                var obj = JsonConvert.DeserializeObject<Newtonsoft.Json.Linq.JObject>(json);
                return obj?["mode"]?.ToString();
            }
            catch { }
            return null;
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
        /// Returns Commands path based on assembly location (thư mục chứa DLL). Để Settings thử khi path chính không có command set.
        /// </summary>
        public static string GetAssemblyCommandsPath()
        {
            string assemblyDir = GetAssemblyDirectory();
            if (string.IsNullOrEmpty(assemblyDir)) return null;
            string commandsPath = Path.Combine(assemblyDir, "Commands");
            return Directory.Exists(commandsPath) ? commandsPath : null;
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
        /// Prefers path that has non-empty registry; otherwise returns any existing Commands dir for discovery.
        /// </summary>
        public static string TryGetRevitAddinsCommandsPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string[] revitVersions = { "2025", "2024", "2026", "2023" };
            string fallback = null;
            foreach (var ver in revitVersions)
            {
                string commandsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins", ver, "revit_mcp_plugin", "Commands");
                if (!Directory.Exists(commandsDir)) continue;
                string registryFile = Path.Combine(commandsDir, "commandRegistry.json");
                if (File.Exists(registryFile) && !IsRegistryEmpty(registryFile))
                    return commandsDir;
                fallback ??= commandsDir;
            }
            return fallback;
        }

        /// <summary>
        /// Dev: path ghi lúc build (bin\...\Commands) để Settings tìm command set khi chạy từ Add-in Manager.
        /// </summary>
        public static string TryGetDevCommandsPathFromFile()
        {
            try
            {
                string file = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "DeepBim-MCP", "dev-commands-path.txt");
                if (!File.Exists(file)) return null;
                string path = File.ReadAllText(file)?.Trim();
                if (string.IsNullOrEmpty(path) || !Directory.Exists(path)) return null;
                return path;
            }
            catch { }
            return null;
        }

        /// <summary>
        /// Returns candidate Commands paths to try when loading command sets.
        /// Order: assembly, dev path file (bin khi dev), Revit Addins, plugin default.
        /// </summary>
        public static string[] GetCandidateCommandsPaths()
        {
            var list = new System.Collections.Generic.List<string>();
            string a = GetAssemblyCommandsPath();
            if (!string.IsNullOrEmpty(a)) list.Add(a);
            string dev = TryGetDevCommandsPathFromFile();
            if (!string.IsNullOrEmpty(dev) && !list.Contains(dev, StringComparer.OrdinalIgnoreCase)) list.Add(dev);
            string r = TryGetRevitAddinsCommandsPath();
            if (!string.IsNullOrEmpty(r) && !list.Contains(r, StringComparer.OrdinalIgnoreCase)) list.Add(r);
            string g = GetCommandsDirectoryPath();
            if (!string.IsNullOrEmpty(g) && !list.Contains(g, StringComparer.OrdinalIgnoreCase)) list.Add(g);
            return list.ToArray();
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
