using System.IO;
using Autodesk.Revit.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver.Native;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Process-wide state shared by all rcd_* commands: main window handle, settings, logging,
    /// and one-time event hook registration (must happen inside a Revit API context).
    /// </summary>
    public static class RcdRuntime
    {
        private static readonly object _lock = new object();
        private static IntPtr _mainHwnd = IntPtr.Zero;
        private static bool _hooksInstalled;
        private static string _revitVersion;
        private static JObject _settings;
        private static DateTime _settingsLoadedUtc = DateTime.MinValue;

        public static string AppDataDir
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DeepBim-MCP");
                try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        public static string RcdDir
        {
            get
            {
                string dir = Path.Combine(AppDataDir, "rcd");
                try { if (!Directory.Exists(dir)) Directory.CreateDirectory(dir); } catch { }
                return dir;
            }
        }

        public static string RevitVersion => _revitVersion ?? "unknown";

        /// <summary>
        /// Main Revit window handle. Set from an API context (EnsureHooks); falls back to the
        /// process main window so socket-thread commands still work before the first API call.
        /// </summary>
        public static IntPtr MainHwnd
        {
            get
            {
                if (_mainHwnd != IntPtr.Zero && Win32.IsWindow(_mainHwnd)) return _mainHwnd;
                try
                {
                    var h = System.Diagnostics.Process.GetCurrentProcess().MainWindowHandle;
                    if (h != IntPtr.Zero) return h;
                }
                catch { }
                return _mainHwnd;
            }
        }

        public static bool HooksInstalled => _hooksInstalled;

        /// <summary>
        /// Call from inside an IExternalEventHandler.Execute (valid API context). Idempotent.
        /// Captures MainWindowHandle, Revit version, and subscribes DocumentChanged + DialogBoxShowing.
        /// </summary>
        public static void EnsureHooks(UIApplication uiapp)
        {
            lock (_lock)
            {
                try { _mainHwnd = uiapp.MainWindowHandle; } catch (Exception ex) { Log("EnsureHooks: MainWindowHandle failed: " + ex.Message); }
                try { _revitVersion = uiapp.Application.VersionNumber; } catch { }

                if (_hooksInstalled) return;
                try
                {
                    ChangeTracker.Subscribe(uiapp);
                    DialogPolicy.Subscribe(uiapp);
                    _hooksInstalled = true;
                    Log($"Hooks installed. revit={_revitVersion} mainHwnd={_mainHwnd}");
                }
                catch (Exception ex)
                {
                    Log("EnsureHooks failed: " + ex);
                }
            }
        }

        // ── Settings (optional override file %APPDATA%\DeepBim-MCP\rcd\rcd-settings.json) ──

        public static JObject Settings
        {
            get
            {
                lock (_lock)
                {
                    if (_settings != null && (DateTime.UtcNow - _settingsLoadedUtc).TotalSeconds < 10) return _settings;
                    var defaults = new JObject
                    {
                        ["enabled"] = true,
                        ["inputBackend"] = "sendinput",
                        ["maxMmPerPixel"] = 5.0,
                        ["interStepDelayMs"] = 60,
                        ["clickSettleMs"] = 40,
                        ["lockTtlMs"] = 120000,
                        ["allowForegroundSteal"] = true,
                        ["requireForeground"] = true,
                        ["statusLocale"] = "en",
                        ["verboseLog"] = true
                    };
                    try
                    {
                        string path = Path.Combine(RcdDir, "rcd-settings.json");
                        if (File.Exists(path))
                        {
                            var user = JObject.Parse(File.ReadAllText(path));
                            defaults.Merge(user, new JsonMergeSettings { MergeArrayHandling = MergeArrayHandling.Replace });
                        }
                    }
                    catch (Exception ex) { Log("Settings load failed: " + ex.Message); }
                    _settings = defaults;
                    _settingsLoadedUtc = DateTime.UtcNow;
                    return _settings;
                }
            }
        }

        public static T Setting<T>(string key, T fallback)
        {
            try
            {
                var tok = Settings[key];
                return tok == null ? fallback : tok.ToObject<T>();
            }
            catch { return fallback; }
        }

        public static void AssertEnabled()
        {
            if (!Setting("enabled", true))
                throw new Models.DriverException(Models.RcdErrorCodes.DriverDisabled, "Revit Command Driver is disabled in rcd-settings.json.");
        }

        // ── Embedded data files (Driver/Data/*.json) with optional user override ──

        public static JToken LoadData(string fileName)
        {
            try
            {
                string userPath = Path.Combine(RcdDir, fileName);
                if (File.Exists(userPath)) return JToken.Parse(File.ReadAllText(userPath));
            }
            catch (Exception ex) { Log($"LoadData user override {fileName} failed: {ex.Message}"); }

            try
            {
                var asm = typeof(RcdRuntime).Assembly;
                string resName = asm.GetManifestResourceNames()
                    .FirstOrDefault(n => n.EndsWith("." + fileName, StringComparison.OrdinalIgnoreCase));
                if (resName == null) { Log($"LoadData: embedded resource {fileName} not found"); return null; }
                using var s = asm.GetManifestResourceStream(resName);
                using var r = new StreamReader(s);
                return JToken.Parse(r.ReadToEnd());
            }
            catch (Exception ex)
            {
                Log($"LoadData embedded {fileName} failed: {ex.Message}");
                return null;
            }
        }

        // ── Logging: %APPDATA%\DeepBim-MCP\Logs\rcd-yyyyMMdd.log ──

        private static readonly object _logLock = new object();

        public static void Log(string message)
        {
            try
            {
                string dir = Path.Combine(AppDataDir, "Logs");
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string path = Path.Combine(dir, $"rcd-{DateTime.Now:yyyyMMdd}.log");
                string line = $"{DateTime.Now:HH:mm:ss.fff} [T{Thread.CurrentThread.ManagedThreadId}] {message}";
                lock (_logLock) File.AppendAllText(path, line + Environment.NewLine);
                System.Diagnostics.Debug.WriteLine("[RCD] " + line);
            }
            catch { }
        }

        public static void LogVerbose(string message)
        {
            if (Setting("verboseLog", true)) Log(message);
        }

        // ── Result helpers (camelCase JSON like the rest of the command set) ──

        public static object Ok(object response, string message = null)
            => new { success = true, message = message ?? string.Empty, response };

        public static object Fail(string code, string message, object data = null)
            => new { success = false, errorCode = code, message, response = data };

        public static object Fail(Models.DriverException ex) => Fail(ex.Code, ex.Message, ex.Data2);

        public static object FailFromException(Exception ex)
        {
            if (ex is Models.DriverException dex) return Fail(dex);
            Log("Unhandled: " + ex);
            return Fail(Models.RcdErrorCodes.InternalError, ex.GetType().Name + ": " + ex.Message);
        }
    }
}
