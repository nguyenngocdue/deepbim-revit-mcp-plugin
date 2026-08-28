using RevitMCPCommandSet.Driver.Models;
using RevitMCPCommandSet.Driver.Native;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Enumerates Revit-owned dialogs and reports foreground state. Pure Win32.
    /// </summary>
    public static class WindowProbe
    {
        public class ForegroundInfo
        {
            public bool IsRevit { get; set; }
            public long Hwnd { get; set; }
            public string Title { get; set; }
        }

        public static ForegroundInfo Foreground()
        {
            var h = Win32.GetForegroundWindow();
            return new ForegroundInfo
            {
                Hwnd = (long)h,
                IsRevit = Win32.BelongsToCurrentProcess(h),
                Title = Win32.GetWindowTextSafe(h)
            };
        }

        /// <summary>
        /// Visible top-level windows of this process other than the main window and our own
        /// modeless MCP windows. Revit modal dialogs (TaskDialog, warnings, Save, Sync…) show up here.
        /// </summary>
        public static List<DialogInfo> FindDialogs()
        {
            var result = new List<DialogInfo>();
            var main = RcdRuntime.MainHwnd;
            foreach (var h in Win32.EnumTopLevel())
            {
                try
                {
                    if (h == main || !Win32.IsWindowVisible(h) || !Win32.BelongsToCurrentProcess(h)) continue;
                    string title = Win32.GetWindowTextSafe(h);
                    string cls = Win32.GetClassNameSafe(h);
                    if (string.IsNullOrEmpty(title)) continue;
                    if (IsOurOwnWindow(title)) continue;
                    // Tool windows / palettes are owned by main but are not modal; still report them —
                    // the AI decides. Filter obvious non-dialogs by class name.
                    if (cls.StartsWith("Afx:") && title.StartsWith("Autodesk Revit", StringComparison.OrdinalIgnoreCase) && Win32.GetWindow(h, Win32.GW_OWNER) == IntPtr.Zero)
                        continue; // secondary main frame (e.g. second monitor view window)

                    var info = new DialogInfo { Hwnd = (long)h, ClassName = cls, Title = title };
                    CollectChildren(h, info);
                    result.Add(info);
                }
                catch { }
            }
            return result;
        }

        private static bool IsOurOwnWindow(string title)
        {
            return title.IndexOf("DeepBim", StringComparison.OrdinalIgnoreCase) >= 0
                || title.IndexOf("MCP", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void CollectChildren(IntPtr dialog, DialogInfo info)
        {
            var texts = new List<string>();
            foreach (var c in Win32.EnumChildren(dialog))
            {
                string cls = Win32.GetClassNameSafe(c);
                string txt = Win32.GetWindowTextSafe(c);
                if (string.IsNullOrWhiteSpace(txt)) continue;
                if (cls.Equals("Button", StringComparison.OrdinalIgnoreCase) || cls.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0)
                    info.Buttons.Add(txt.Replace("&", string.Empty));
                else if (cls.Equals("Static", StringComparison.OrdinalIgnoreCase) || cls.IndexOf("Static", StringComparison.OrdinalIgnoreCase) >= 0 || cls.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0)
                    texts.Add(txt);
            }
            info.Text = string.Join(" | ", texts.Take(6));
        }

        /// <summary>Clicks a Win32 button by caption inside a dialog. Returns false if not found (WPF dialogs).</summary>
        public static bool ClickButton(IntPtr dialog, string caption)
        {
            foreach (var c in Win32.EnumChildren(dialog))
            {
                string txt = Win32.GetWindowTextSafe(c).Replace("&", string.Empty);
                if (txt.Equals(caption, StringComparison.OrdinalIgnoreCase) && Win32.GetClassNameSafe(c).IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    Win32.PostMessage(c, Win32.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                    return true;
                }
            }
            return false;
        }

        public static void Close(IntPtr dialog) => Win32.PostMessage(dialog, Win32.WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

        public static int[] MainWindowRect()
        {
            var main = RcdRuntime.MainHwnd;
            if (main == IntPtr.Zero || !Win32.GetWindowRect(main, out var r)) return null;
            return new[] { r.Left, r.Top, r.Right, r.Bottom };
        }
    }
}
