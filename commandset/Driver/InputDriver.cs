using System.Diagnostics;
using System.Text.RegularExpressions;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPCommandSet.Driver.Native;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Executes an InputStep batch against Revit using Win32 SendInput (mouse + keyboard) and the
    /// mapping captured by rcd_post_command. Runs on the socket thread — never touches the Revit API.
    /// </summary>
    public class InputDriver
    {
        public class BatchOptions
        {
            public bool StopOnDialog = true;
            public bool StopOnStatusMismatch = false;
            public int InterStepDelayMs = 60;
            public bool DryRun = false;
        }

        public class BatchResult
        {
            public int Completed;
            public List<InputStepResult> Steps = new List<InputStepResult>();
            public string StatusFinal;
            public bool Idle;
            public DialogInfo Dialog;
            public string ErrorCode;
            public string Error;
        }

        private readonly ViewMapping _mapping;
        private readonly BatchOptions _opt;
        private readonly int _clickSettleMs;
        private readonly bool _requireForeground;
        private readonly bool _allowForegroundSteal;
        private HashSet<long> _baselineDialogs;

        public InputDriver(ViewMapping mapping, BatchOptions options)
        {
            _mapping = mapping;
            _opt = options ?? new BatchOptions();
            _clickSettleMs = RcdRuntime.Setting("clickSettleMs", 40);
            _requireForeground = RcdRuntime.Setting("requireForeground", true);
            _allowForegroundSteal = RcdRuntime.Setting("allowForegroundSteal", true);
        }

        public BatchResult Run(IList<InputStep> steps, long marker)
        {
            var result = new BatchResult();
            _baselineDialogs = new HashSet<long>(WindowProbe.FindDialogs().Select(d => d.Hwnd));
            DriverLock.ClearAbort();

            for (int i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var sr = new InputStepResult { Index = i, Type = step.Type };
                var sw = Stopwatch.StartNew();
                try
                {
                    if (DriverLock.AbortRequested)
                        throw new DriverException(RcdErrorCodes.UserAborted, "Abort requested (rcd_ui_cancel or user).");

                    ExecuteStep(step, sr, marker);

                    if (!_opt.DryRun && _opt.InterStepDelayMs > 0) Thread.Sleep(_opt.InterStepDelayMs);
                    var status = StatusBarReader.Read();
                    sr.StatusAfter = status.Text;
                    sr.Ok = true;

                    if (!_opt.DryRun)
                    {
                        // New dialog since batch start?
                        var dialogs = WindowProbe.FindDialogs().Where(d => !_baselineDialogs.Contains(d.Hwnd)).ToList();
                        if (dialogs.Count > 0 && _opt.StopOnDialog)
                        {
                            result.Dialog = dialogs[0];
                            throw new DriverException(RcdErrorCodes.DialogOpen, $"A dialog opened after step {i}: '{dialogs[0].Title}'. Batch stopped.", dialogs[0]);
                        }
                        if (!string.IsNullOrEmpty(step.ExpectStatus) && _opt.StopOnStatusMismatch &&
                            status.Text.IndexOf(step.ExpectStatus, StringComparison.OrdinalIgnoreCase) < 0)
                            throw new DriverException(RcdErrorCodes.StatusMismatch, $"After step {i} status is '{status.Text}', expected to contain '{step.ExpectStatus}'.");
                    }

                    sr.ElapsedMs = sw.ElapsedMilliseconds;
                    result.Steps.Add(sr);
                    result.Completed = i + 1;
                    RcdRuntime.LogVerbose($"step {i} {step.Type} ok screen={(sr.Screen == null ? "-" : sr.Screen[0] + "," + sr.Screen[1])} status='{sr.StatusAfter}'");
                }
                catch (DriverException dex)
                {
                    sr.Ok = false; sr.Error = dex.Message; sr.ErrorCode = dex.Code; sr.ElapsedMs = sw.ElapsedMilliseconds;
                    sr.StatusAfter ??= StatusBarReader.ReadText();
                    result.Steps.Add(sr);
                    result.ErrorCode = dex.Code; result.Error = dex.Message;
                    RcdRuntime.Log($"step {i} {step.Type} FAILED {dex.Code}: {dex.Message}");
                    break;
                }
                catch (Exception ex)
                {
                    sr.Ok = false; sr.Error = ex.Message; sr.ErrorCode = RcdErrorCodes.InternalError; sr.ElapsedMs = sw.ElapsedMilliseconds;
                    result.Steps.Add(sr);
                    result.ErrorCode = RcdErrorCodes.InternalError; result.Error = ex.Message;
                    RcdRuntime.Log($"step {i} {step.Type} EXCEPTION: {ex}");
                    break;
                }
            }

            var fin = StatusBarReader.Read();
            result.StatusFinal = fin.Text;
            result.Idle = fin.IsIdle;
            if (result.Dialog == null)
                result.Dialog = WindowProbe.FindDialogs().FirstOrDefault(d => !_baselineDialogs.Contains(d.Hwnd));
            return result;
        }

        // ── step dispatch ────────────────────────────────────────────────────

        private void ExecuteStep(InputStep step, InputStepResult sr, long marker)
        {
            switch ((step.Type ?? string.Empty).ToLowerInvariant())
            {
                case "click": DoClick(step, sr, 1); break;
                case "dblclick": DoClick(step, sr, 2); break;
                case "move": { var p = ResolvePoint(step.Point, step.Screen, sr); if (!_opt.DryRun) { EnsureForeground(); WithShift(step.HoldShift, () => MouseMove(p.x, p.y)); } break; }
                case "drag": DoDrag(step, sr); break;
                case "type":
                    if (string.IsNullOrEmpty(step.Text) && !step.Enter) throw new DriverException(RcdErrorCodes.InvalidStep, "type: 'text' is required.");
                    if (!_opt.DryRun) { EnsureForeground(); TypeText(step.Text ?? string.Empty); if (step.Enter) KeyPress(Win32.VK_RETURN); }
                    break;
                case "key":
                    {
                        int vk = ResolveKey(step.Key);
                        if (!_opt.DryRun)
                        {
                            EnsureForeground();
                            var mods = (step.Modifiers ?? new List<string>()).Select(ResolveKey).ToList();
                            foreach (var m in mods) KeyDown(m);
                            try { for (int k = 0; k < Math.Max(1, step.Times); k++) { KeyPress(vk); if (step.Times > 1) Thread.Sleep(80); } }
                            finally { foreach (var m in mods.AsEnumerable().Reverse()) KeyUp(m); }
                        }
                        break;
                    }
                case "wait":
                    if (!_opt.DryRun) Thread.Sleep(Math.Min(Math.Max(step.Ms, 0), 5000));
                    break;
                case "waitstatus": WaitStatus(step, sr); break;
                case "waitchanges": WaitChanges(step, marker); break;
                default:
                    throw new DriverException(RcdErrorCodes.InvalidStep, $"Unknown step type '{step.Type}'. Allowed: click, dblclick, move, drag, type, key, wait, waitStatus, waitChanges.");
            }
        }

        private void DoClick(InputStep step, InputStepResult sr, int count)
        {
            var p = ResolvePoint(step.Point, step.Screen, sr);
            if (_opt.DryRun) return;
            EnsureForeground();
            WithShift(step.HoldShift, () =>
            {
                MouseMove(p.x, p.y);
                Thread.Sleep(_clickSettleMs);
                if (!string.IsNullOrEmpty(step.SnapOverride))
                {
                    // Snap override shortcuts (SO, SE, SM, SI, SC, SP…) apply to the next click only.
                    TypeText(step.SnapOverride.ToUpperInvariant());
                    Thread.Sleep(60);
                }
                for (int i = 0; i < count; i++)
                {
                    MouseClick(step.Button);
                    if (count > 1) Thread.Sleep(60);
                }
            });
        }

        private void DoDrag(InputStep step, InputStepResult sr)
        {
            if (step.From == null || step.To == null) throw new DriverException(RcdErrorCodes.InvalidStep, "drag: 'from' and 'to' are required.");
            var a = ResolvePoint(step.From, null, sr);
            var b = ResolvePoint(step.To, null, null);
            if (_opt.DryRun) return;
            EnsureForeground();
            WithShift(step.HoldShift, () =>
            {
                MouseMove(a.x, a.y); Thread.Sleep(_clickSettleMs);
                MouseButton(step.Button, true); Thread.Sleep(60);
                // a few intermediate moves so Revit registers a drag
                for (int i = 1; i <= 4; i++) { MouseMove(a.x + (b.x - a.x) * i / 4, a.y + (b.y - a.y) * i / 4); Thread.Sleep(30); }
                MouseButton(step.Button, false);
            });
        }

        private void WaitStatus(InputStep step, InputStepResult sr)
        {
            if (string.IsNullOrEmpty(step.Contains) && string.IsNullOrEmpty(step.Regex))
                throw new DriverException(RcdErrorCodes.InvalidStep, "waitStatus: 'contains' or 'regex' is required.");
            int timeout = step.TimeoutMs > 0 ? Math.Min(step.TimeoutMs, 15000) : 5000;
            if (_opt.DryRun) return;
            var sw = Stopwatch.StartNew();
            Regex re = string.IsNullOrEmpty(step.Regex) ? null : new Regex(step.Regex, RegexOptions.IgnoreCase);
            string last = string.Empty;
            while (sw.ElapsedMilliseconds < timeout)
            {
                if (DriverLock.AbortRequested) throw new DriverException(RcdErrorCodes.UserAborted, "Abort requested.");
                last = StatusBarReader.ReadText();
                bool ok = re != null ? re.IsMatch(last) : last.IndexOf(step.Contains, StringComparison.OrdinalIgnoreCase) >= 0;
                if (ok) return;
                Thread.Sleep(50);
            }
            throw new DriverException(RcdErrorCodes.StatusTimeout, $"Status bar did not show '{step.Contains ?? step.Regex}' within {timeout} ms. Last status: '{last}'.", new { lastStatus = last });
        }

        private void WaitChanges(InputStep step, long marker)
        {
            int timeout = step.TimeoutMs > 0 ? Math.Min(step.TimeoutMs, 15000) : 5000;
            if (_opt.DryRun) return;
            var sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < timeout)
            {
                var cs = ChangeTracker.Since(marker, 50);
                if (cs.Added.Count >= Math.Max(1, step.MinAdded)) return;
                Thread.Sleep(50);
            }
            throw new DriverException(RcdErrorCodes.StatusTimeout, $"No new elements ({step.MinAdded}) were created within {timeout} ms.");
        }

        // ── coordinates ──────────────────────────────────────────────────────

        private (double x, double y) ResolvePoint(double[] point, double[] screen, InputStepResult sr)
        {
            double px, py;
            if (screen != null && screen.Length >= 2) { px = screen[0]; py = screen[1]; }
            else if (point != null && point.Length >= 2)
            {
                if (_mapping == null)
                    throw new DriverException(RcdErrorCodes.NoMapping, "No view mapping. Call rcd_post_command (which captures the mapping) before using model-space points, or pass 'screen' pixels.");
                var r = _mapping.ToScreen(point[0], point[1], point.Length > 2 ? point[2] : 0);
                px = r.px; py = r.py;
                if (!_mapping.IsOnScreen(px, py))
                    throw new DriverException(RcdErrorCodes.PointOffScreen, $"Point ({point[0]},{point[1]}) maps to pixel ({px:F0},{py:F0}) outside the view rect [{string.Join(",", _mapping.ScreenRect)}]. Re-post with prepare.fitPoints covering this point.",
                        new { pixel = new[] { px, py }, screenRect = _mapping.ScreenRect });
            }
            else throw new DriverException(RcdErrorCodes.InvalidStep, $"{(sr?.Type ?? "step")}: 'point' [x,y,z] mm or 'screen' [px,py] is required.");

            if (sr != null) sr.Screen = new[] { (int)Math.Round(px), (int)Math.Round(py) };
            return (px, py);
        }

        // ── foreground ───────────────────────────────────────────────────────

        public static bool EnsureForeground(bool allowSteal = true, int waitMs = 600)
        {
            var main = RcdRuntime.MainHwnd;
            if (main == IntPtr.Zero) return false;
            if (Win32.BelongsToCurrentProcess(Win32.GetForegroundWindow())) return true;
            if (!allowSteal) return false;

            try
            {
                if (Win32.IsIconic(main)) Win32.ShowWindow(main, Win32.SW_RESTORE);
                uint fgThread = Win32.GetWindowThreadProcessId(Win32.GetForegroundWindow(), out _);
                uint me = Win32.GetCurrentThreadId();
                bool attached = fgThread != 0 && fgThread != me && Win32.AttachThreadInput(me, fgThread, true);
                try
                {
                    Win32.BringWindowToTop(main);
                    Win32.SetForegroundWindow(main);
                }
                finally { if (attached) Win32.AttachThreadInput(me, fgThread, false); }

                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < waitMs)
                {
                    if (Win32.BelongsToCurrentProcess(Win32.GetForegroundWindow())) return true;
                    Thread.Sleep(30);
                }
                // Last resort: ALT-tap trick then SwitchToThisWindow
                KeyPress(Win32.VK_MENU);
                Win32.SwitchToThisWindow(main, true);
                Thread.Sleep(150);
                return Win32.BelongsToCurrentProcess(Win32.GetForegroundWindow());
            }
            catch (Exception ex)
            {
                RcdRuntime.Log("EnsureForeground failed: " + ex.Message);
                return false;
            }
        }

        private void EnsureForeground()
        {
            if (!_requireForeground) return;
            if (!EnsureForeground(_allowForegroundSteal))
                throw new DriverException(RcdErrorCodes.ForegroundFailed, "Revit is not the foreground window and could not be brought to front. Bring Revit to front (or set driver.allowForegroundSteal) and retry.",
                    new { foreground = WindowProbe.Foreground() });
        }

        // ── SendInput primitives ─────────────────────────────────────────────

        private static void Send(params Win32.INPUT[] inputs)
        {
            uint sent = Win32.SendInput((uint)inputs.Length, inputs, Win32.InputSize);
            if (sent != inputs.Length)
                RcdRuntime.Log($"SendInput sent {sent}/{inputs.Length} (err {System.Runtime.InteropServices.Marshal.GetLastWin32Error()})");
        }

        public static void MouseMove(double px, double py)
        {
            int vx = Win32.GetSystemMetrics(Win32.SM_XVIRTUALSCREEN), vy = Win32.GetSystemMetrics(Win32.SM_YVIRTUALSCREEN);
            int vw = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CXVIRTUALSCREEN)), vh = Math.Max(1, Win32.GetSystemMetrics(Win32.SM_CYVIRTUALSCREEN));
            int nx = (int)Math.Round((px - vx) * 65535.0 / vw);
            int ny = (int)Math.Round((py - vy) * 65535.0 / vh);
            var inp = new Win32.INPUT { type = Win32.INPUT_MOUSE };
            inp.u.mi = new Win32.MOUSEINPUT { dx = nx, dy = ny, dwFlags = Win32.MOUSEEVENTF_MOVE | Win32.MOUSEEVENTF_ABSOLUTE | Win32.MOUSEEVENTF_VIRTUALDESK };
            Send(inp);
            // Belt and braces: absolute SendInput can be off by a pixel on some DPI setups.
            Win32.SetCursorPos((int)Math.Round(px), (int)Math.Round(py));
        }

        private static void MouseButton(string button, bool down)
        {
            uint flag = (button ?? "left").ToLowerInvariant() switch
            {
                "right" => down ? Win32.MOUSEEVENTF_RIGHTDOWN : Win32.MOUSEEVENTF_RIGHTUP,
                "middle" => down ? Win32.MOUSEEVENTF_MIDDLEDOWN : Win32.MOUSEEVENTF_MIDDLEUP,
                _ => down ? Win32.MOUSEEVENTF_LEFTDOWN : Win32.MOUSEEVENTF_LEFTUP
            };
            var inp = new Win32.INPUT { type = Win32.INPUT_MOUSE };
            inp.u.mi = new Win32.MOUSEINPUT { dwFlags = flag };
            Send(inp);
        }

        private static void MouseClick(string button)
        {
            MouseButton(button, true);
            Thread.Sleep(20);
            MouseButton(button, false);
        }

        public static void KeyDown(int vk) => Send(KeyInput((ushort)vk, false));
        public static void KeyUp(int vk) => Send(KeyInput((ushort)vk, true));
        public static void KeyPress(int vk) { KeyDown(vk); Thread.Sleep(15); KeyUp(vk); }

        private static Win32.INPUT KeyInput(ushort vk, bool up)
        {
            var inp = new Win32.INPUT { type = Win32.INPUT_KEYBOARD };
            ushort scan = (ushort)Win32.MapVirtualKey(vk, 0);
            uint flags = up ? Win32.KEYEVENTF_KEYUP : 0;
            if (IsExtended(vk)) flags |= Win32.KEYEVENTF_EXTENDEDKEY;
            inp.u.ki = new Win32.KEYBDINPUT { wVk = vk, wScan = scan, dwFlags = flags };
            return inp;
        }

        private static bool IsExtended(int vk) => vk == Win32.VK_LEFT || vk == Win32.VK_RIGHT || vk == Win32.VK_UP || vk == Win32.VK_DOWN || vk == Win32.VK_HOME || vk == Win32.VK_END || vk == Win32.VK_DELETE;

        /// <summary>Types text as real key presses (so Revit shortcuts and listening dimensions see WM_KEYDOWN + WM_CHAR).</summary>
        public static void TypeText(string text)
        {
            foreach (char c in text)
            {
                if (c == '\n' || c == '\r') { KeyPress(Win32.VK_RETURN); continue; }
                short scan = Win32.VkKeyScanW(c);
                if (scan == -1)
                {
                    // Not on this keyboard layout → unicode packet
                    var d = new Win32.INPUT { type = Win32.INPUT_KEYBOARD }; d.u.ki = new Win32.KEYBDINPUT { wScan = c, dwFlags = Win32.KEYEVENTF_UNICODE };
                    var u = new Win32.INPUT { type = Win32.INPUT_KEYBOARD }; u.u.ki = new Win32.KEYBDINPUT { wScan = c, dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP };
                    Send(d, u);
                }
                else
                {
                    int vk = scan & 0xFF; int shiftState = (scan >> 8) & 0xFF;
                    bool shift = (shiftState & 1) != 0, ctrl = (shiftState & 2) != 0, alt = (shiftState & 4) != 0;
                    if (shift) KeyDown(Win32.VK_SHIFT); if (ctrl) KeyDown(Win32.VK_CONTROL); if (alt) KeyDown(Win32.VK_MENU);
                    KeyPress(vk);
                    if (alt) KeyUp(Win32.VK_MENU); if (ctrl) KeyUp(Win32.VK_CONTROL); if (shift) KeyUp(Win32.VK_SHIFT);
                }
                Thread.Sleep(25);
            }
        }

        private static void WithShift(bool hold, Action body)
        {
            if (!hold) { body(); return; }
            KeyDown(Win32.VK_SHIFT);
            try { Thread.Sleep(20); body(); }
            finally { Thread.Sleep(20); KeyUp(Win32.VK_SHIFT); }
        }

        public static int ResolveKey(string key)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new DriverException(RcdErrorCodes.InvalidStep, "key: 'key' is required.");
            string k = key.Trim();
            switch (k.ToLowerInvariant())
            {
                case "enter": case "return": return Win32.VK_RETURN;
                case "escape": case "esc": return Win32.VK_ESCAPE;
                case "tab": return Win32.VK_TAB;
                case "space": return Win32.VK_SPACE;
                case "delete": case "del": return Win32.VK_DELETE;
                case "backspace": return Win32.VK_BACK;
                case "shift": return Win32.VK_SHIFT;
                case "ctrl": case "control": return Win32.VK_CONTROL;
                case "alt": return Win32.VK_MENU;
                case "up": return Win32.VK_UP;
                case "down": return Win32.VK_DOWN;
                case "left": return Win32.VK_LEFT;
                case "right": return Win32.VK_RIGHT;
                case "home": return Win32.VK_HOME;
                case "end": return Win32.VK_END;
            }
            if (k.Length == 1) { char c = char.ToUpperInvariant(k[0]); if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')) return c; }
            if ((k.StartsWith("F") || k.StartsWith("f")) && int.TryParse(k.Substring(1), out int fn) && fn >= 1 && fn <= 12) return Win32.VK_F1 + fn - 1;
            throw new DriverException(RcdErrorCodes.InvalidStep, $"Unknown key '{key}'.");
        }

        // ── cancel helpers (used by rcd_ui_cancel; no mapping needed) ────────

        /// <summary>Sends Escape via PostMessage to the main window (no foreground needed) and, if allowed, via SendInput.</summary>
        public static void SendEscape(int times, bool alsoSendInput)
        {
            var main = RcdRuntime.MainHwnd;
            for (int i = 0; i < times; i++)
            {
                if (main != IntPtr.Zero)
                {
                    Win32.PostMessage(main, Win32.WM_KEYDOWN, (IntPtr)Win32.VK_ESCAPE, IntPtr.Zero);
                    Win32.PostMessage(main, Win32.WM_KEYUP, (IntPtr)Win32.VK_ESCAPE, (IntPtr)0xC0000000);
                }
                Thread.Sleep(80);
            }
            if (alsoSendInput && EnsureForeground(true, 300))
            {
                for (int i = 0; i < times; i++) { KeyPress(Win32.VK_ESCAPE); Thread.Sleep(80); }
            }
        }
    }
}
