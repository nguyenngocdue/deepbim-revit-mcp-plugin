using System.Text.RegularExpressions;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver.Models;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Observes Revit dialogs (DialogBoxShowing) and optionally auto-answers them based on
    /// session rules set by the AI through rcd_dialog_policy. Default behaviour is observe-only
    /// except for the whitelist in Data/rcd-dialog-defaults.json.
    /// </summary>
    public static class DialogPolicy
    {
        private static readonly object _lock = new object();
        private static readonly List<DialogRule> _rules = new List<DialogRule>();
        private static readonly LinkedList<DialogEvent> _events = new LinkedList<DialogEvent>();
        private static List<DialogRule> _defaults;
        private static bool _subscribed;
        private const int MaxEvents = 200;

        public static void Subscribe(UIApplication uiapp)
        {
            lock (_lock)
            {
                if (_subscribed) return;
                uiapp.DialogBoxShowing += OnDialogBoxShowing;
                _subscribed = true;
            }
        }

        private static List<DialogRule> Defaults
        {
            get
            {
                if (_defaults != null) return _defaults;
                try
                {
                    var tok = RcdRuntime.LoadData("rcd-dialog-defaults.json");
                    _defaults = tok?["rules"]?.ToObject<List<DialogRule>>() ?? new List<DialogRule>();
                }
                catch { _defaults = new List<DialogRule>(); }
                return _defaults;
            }
        }

        public static void SetRules(IEnumerable<DialogRule> rules, int ttlMs)
        {
            lock (_lock)
            {
                foreach (var r in rules)
                {
                    if (ttlMs > 0) r.ExpiresUtc = DateTime.UtcNow.AddMilliseconds(ttlMs);
                    _rules.Add(r);
                }
            }
        }

        public static void ClearRules() { lock (_lock) _rules.Clear(); }

        public static List<DialogRule> ListRules()
        {
            lock (_lock)
            {
                Prune();
                return _rules.ToList();
            }
        }

        public static List<DialogEvent> RecentEvents(int max = 50)
        {
            lock (_lock) return _events.Reverse().Take(max).Reverse().ToList();
        }

        public static List<DialogEvent> EventsSince(DateTime utc)
        {
            lock (_lock) return _events.Where(e => e.Utc >= utc).ToList();
        }

        private static void Prune()
        {
            var now = DateTime.UtcNow;
            _rules.RemoveAll(r => r.Consumed || (r.ExpiresUtc.HasValue && r.ExpiresUtc.Value < now));
        }

        private static void OnDialogBoxShowing(object sender, DialogBoxShowingEventArgs e)
        {
            string kind = e.GetType().Name;
            string dialogId = null, message = null;
            try
            {
                if (e is TaskDialogShowingEventArgs td) { dialogId = td.DialogId; message = td.Message; }
                else if (e is MessageBoxShowingEventArgs mb) { message = mb.Message; dialogId = "MessageBox:" + mb.DialogType; }
                else { try { dialogId = e.DialogId; } catch { } }
            }
            catch { }

            var evt = new DialogEvent { Utc = DateTime.UtcNow, Kind = kind, DialogId = dialogId, Message = message, Action = "shown" };

            DialogRule match = null;
            lock (_lock)
            {
                Prune();
                match = _rules.FirstOrDefault(r => Matches(r, dialogId, message))
                        ?? Defaults.FirstOrDefault(r => Matches(r, dialogId, message));
            }

            if (match != null)
            {
                try
                {
                    int code = ResolveResultCode(match.OverrideResult);
                    bool ok = e.OverrideResult(code);
                    evt.Action = ok ? $"overrideResult:{code}" : $"overrideResult:{code}:rejected";
                    if (match.Once) match.Consumed = true;
                }
                catch (Exception ex)
                {
                    evt.Action = "overrideFailed:" + ex.Message;
                }
            }

            lock (_lock)
            {
                _events.AddLast(evt);
                while (_events.Count > MaxEvents) _events.RemoveFirst();
            }
            RcdRuntime.Log($"Dialog {kind} id={dialogId} action={evt.Action} msg={Truncate(message, 200)}");
        }

        private static bool Matches(DialogRule r, string dialogId, string message)
        {
            if (!string.IsNullOrEmpty(r.DialogId))
            {
                if (dialogId == null || !string.Equals(r.DialogId, dialogId, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (!string.IsNullOrEmpty(r.MessageRegex))
            {
                if (message == null || !Regex.IsMatch(message, r.MessageRegex, RegexOptions.IgnoreCase | RegexOptions.Singleline)) return false;
            }
            if (!string.IsNullOrEmpty(r.TitleRegex))
            {
                // DialogBoxShowingEventArgs does not expose a title; match TitleRegex against the dialog id as best effort.
                if (dialogId == null || !Regex.IsMatch(dialogId, r.TitleRegex, RegexOptions.IgnoreCase)) return false;
            }
            return !string.IsNullOrEmpty(r.DialogId) || !string.IsNullOrEmpty(r.MessageRegex) || !string.IsNullOrEmpty(r.TitleRegex);
        }

        /// <summary>Accepts an int, "IDOK"/"IDCANCEL"/..., or { "commandLink": n } (1001 + n - 1).</summary>
        public static int ResolveResultCode(JToken tok)
        {
            if (tok == null) return 1;
            if (tok.Type == JTokenType.Integer) return tok.Value<int>();
            if (tok.Type == JTokenType.Object && tok["commandLink"] != null) return 1000 + tok["commandLink"].Value<int>();
            switch (tok.ToString().Trim().ToUpperInvariant())
            {
                case "IDOK": case "OK": return 1;
                case "IDCANCEL": case "CANCEL": return 2;
                case "IDABORT": return 3;
                case "IDRETRY": return 4;
                case "IDIGNORE": return 5;
                case "IDYES": case "YES": return 6;
                case "IDNO": case "NO": return 7;
                case "IDCLOSE": case "CLOSE": return 8;
                default:
                    return int.TryParse(tok.ToString(), out int n) ? n : 1;
            }
        }

        private static string Truncate(string s, int max) => string.IsNullOrEmpty(s) ? s : (s.Length <= max ? s : s.Substring(0, max) + "…");
    }
}
