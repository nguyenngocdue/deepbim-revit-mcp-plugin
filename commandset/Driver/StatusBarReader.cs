using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using RevitMCPCommandSet.Driver.Native;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Reads the Revit status bar (msctls_statusbar32 child of the main window). The prompt text
    /// ("Click to enter wall start point.") is the driver's primary signal of what Revit awaits.
    /// Pure Win32 — safe on the socket thread, works while a Revit command is active.
    /// </summary>
    public static class StatusBarReader
    {
        private static IntPtr _statusHwnd = IntPtr.Zero;
        private static List<Regex> _idlePatterns;

        public class StatusInfo
        {
            public string Text { get; set; } = string.Empty;
            public bool IsIdle { get; set; }
            public bool Found { get; set; }
        }

        public static StatusInfo Read()
        {
            var info = new StatusInfo();
            try
            {
                var hwnd = FindStatusBar();
                if (hwnd == IntPtr.Zero) return info;
                info.Found = true;
                info.Text = ReadPart(hwnd, 0);
                if (string.IsNullOrWhiteSpace(info.Text))
                {
                    // Some layouts put the prompt in a later part; take the longest non-empty one.
                    int parts = (int)Win32.SendMessage(hwnd, Win32.SB_GETPARTS, IntPtr.Zero, IntPtr.Zero);
                    for (int i = 1; i < Math.Min(parts, 8); i++)
                    {
                        string t = ReadPart(hwnd, i);
                        if (t.Length > info.Text.Length) info.Text = t;
                    }
                }
                info.Text = info.Text?.Trim() ?? string.Empty;
                info.IsIdle = IsIdleText(info.Text);
            }
            catch (Exception ex)
            {
                RcdRuntime.Log("StatusBarReader.Read failed: " + ex.Message);
            }
            return info;
        }

        public static string ReadText() => Read().Text;

        public static bool IsIdleText(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return true;
            foreach (var re in IdlePatterns)
                if (re.IsMatch(text)) return true;
            return false;
        }

        private static List<Regex> IdlePatterns
        {
            get
            {
                if (_idlePatterns != null) return _idlePatterns;
                var list = new List<Regex>();
                try
                {
                    var data = RcdRuntime.LoadData("rcd-status-patterns.json");
                    string locale = RcdRuntime.Setting("statusLocale", "en");
                    var arr = data?[locale]?["idle"] as Newtonsoft.Json.Linq.JArray ?? data?["en"]?["idle"] as Newtonsoft.Json.Linq.JArray;
                    if (arr != null)
                        foreach (var p in arr) list.Add(new Regex(p.ToString(), RegexOptions.IgnoreCase));
                }
                catch { }
                if (list.Count == 0)
                {
                    list.Add(new Regex(@"^Click to select", RegexOptions.IgnoreCase));
                    list.Add(new Regex(@"^Ready$", RegexOptions.IgnoreCase));
                }
                _idlePatterns = list;
                return list;
            }
        }

        private static IntPtr FindStatusBar()
        {
            if (_statusHwnd != IntPtr.Zero && Win32.IsWindow(_statusHwnd)) return _statusHwnd;
            var main = RcdRuntime.MainHwnd;
            if (main == IntPtr.Zero) return IntPtr.Zero;
            foreach (var child in Win32.EnumChildren(main))
            {
                if (Win32.GetClassNameSafe(child).Equals("msctls_statusbar32", StringComparison.OrdinalIgnoreCase))
                {
                    _statusHwnd = child;
                    return child;
                }
            }
            return IntPtr.Zero;
        }

        private static string ReadPart(IntPtr hwnd, int part)
        {
            IntPtr lenRes = Win32.SendMessage(hwnd, Win32.SB_GETTEXTLENGTHW, (IntPtr)part, IntPtr.Zero);
            int len = (int)((long)lenRes & 0xFFFF);
            if (len <= 0) return string.Empty;
            IntPtr buf = Marshal.AllocHGlobal((len + 1) * 2);
            try
            {
                Win32.SendMessage(hwnd, Win32.SB_GETTEXTW, (IntPtr)part, buf);
                return Marshal.PtrToStringUni(buf, len) ?? string.Empty;
            }
            finally { Marshal.FreeHGlobal(buf); }
        }
    }
}
