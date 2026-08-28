using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using Autodesk.Revit.UI;
using Newtonsoft.Json.Linq;
using RevitMCPCommandSet.Driver.Models;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Catalog of every command Revit can post: PostableCommand enum (reflection, so it follows the
    /// running Revit version) + KeyboardShortcuts.xml + built-in default shortcuts + interaction hints
    /// + user-declared add-in commands. Build() must run in an API context; Search/Resolve are pure.
    /// </summary>
    public static class CommandCatalog
    {
        private static readonly object _lock = new object();
        private static List<CommandInfo> _items;
        private static Dictionary<string, RevitCommandId> _idsByName;
        private static string _shortcutsSource;
        private static int _shortcutsMatched;

        public static bool IsBuilt { get { lock (_lock) return _items != null; } }
        public static string ShortcutsSource => _shortcutsSource;
        public static int ShortcutsMatched => _shortcutsMatched;
        public static int Count { get { lock (_lock) return _items?.Count ?? 0; } }

        public static void EnsureBuilt(UIApplication uiapp)
        {
            lock (_lock)
            {
                if (_items != null) return;
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var items = new List<CommandInfo>();
                var ids = new Dictionary<string, RevitCommandId>(StringComparer.OrdinalIgnoreCase);

                // 1) PostableCommand enum → RevitCommandId
                foreach (PostableCommand pc in Enum.GetValues(typeof(PostableCommand)))
                {
                    RevitCommandId cid = null;
                    try { cid = RevitCommandId.LookupPostableCommandId(pc); } catch { }
                    if (cid == null) continue;
                    string name = pc.ToString();
                    if (ids.ContainsKey(name)) continue;
                    ids[name] = cid;
                    items.Add(new CommandInfo
                    {
                        Name = name,
                        Id = cid.Name,
                        Kind = "postable",
                        Words = SplitCamel(name),
                        DisplayName = SplitCamel(name, true)
                    });
                }

                // 2) KeyboardShortcuts.xml (user file) — shortcuts + ribbon path
                var shortcuts = KeyboardShortcutsReader.Load(RcdRuntime.RevitVersion, out _shortcutsSource);
                _shortcutsMatched = 0;
                foreach (var it in items)
                {
                    if (shortcuts.TryGetValue(it.Id, out var e))
                    {
                        _shortcutsMatched++;
                        it.Shortcuts.AddRange(e.Shortcuts.Where(s => !it.Shortcuts.Contains(s, StringComparer.OrdinalIgnoreCase)));
                        if (!string.IsNullOrEmpty(e.Paths)) it.RibbonPath = e.Paths;
                        if (!string.IsNullOrEmpty(e.CommandName)) it.DisplayName = e.CommandName;
                    }
                }

                // 3) Built-in defaults (Data/rcd-default-shortcuts.json) — fill gaps only
                try
                {
                    var defaults = RcdRuntime.LoadData("rcd-default-shortcuts.json") as JObject;
                    if (defaults != null)
                    {
                        var byName = items.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in defaults.Properties())
                        {
                            if (!byName.TryGetValue(prop.Name, out var it)) continue;
                            var o = prop.Value as JObject; if (o == null) continue;
                            var sc = o["shortcuts"]?.ToObject<List<string>>();
                            if (sc != null) foreach (var s in sc) if (!it.Shortcuts.Contains(s, StringComparer.OrdinalIgnoreCase)) it.Shortcuts.Add(s);
                            if (string.IsNullOrEmpty(it.RibbonPath) && o["ribbonPath"] != null) it.RibbonPath = o["ribbonPath"].ToString();
                            if (o["displayName"] != null) it.DisplayName = o["displayName"].ToString();
                        }
                    }
                }
                catch (Exception ex) { RcdRuntime.Log("default shortcuts merge failed: " + ex.Message); }

                // 4) Interaction hints (Data/rcd-interaction-hints.json)
                try
                {
                    var hints = RcdRuntime.LoadData("rcd-interaction-hints.json") as JObject;
                    if (hints != null)
                    {
                        var byName = items.ToDictionary(i => i.Name, StringComparer.OrdinalIgnoreCase);
                        foreach (var prop in hints.Properties())
                            if (byName.TryGetValue(prop.Name, out var it)) it.Interaction = prop.Value.ToString();
                    }
                }
                catch (Exception ex) { RcdRuntime.Log("interaction hints merge failed: " + ex.Message); }

                // 5) User add-in commands (%APPDATA%\DeepBim-MCP\rcd\rcd-commands.user.json)
                try
                {
                    string userFile = Path.Combine(RcdRuntime.RcdDir, "rcd-commands.user.json");
                    if (File.Exists(userFile))
                    {
                        var arr = JToken.Parse(File.ReadAllText(userFile)) as JArray;
                        if (arr != null)
                            foreach (var o in arr.OfType<JObject>())
                            {
                                string alias = o["alias"]?.ToString(); string cmdId = o["commandId"]?.ToString();
                                if (string.IsNullOrEmpty(alias) || string.IsNullOrEmpty(cmdId)) continue;
                                RevitCommandId cid = null;
                                try { cid = RevitCommandId.LookupCommandId(cmdId); } catch { }
                                if (cid == null) { RcdRuntime.Log($"user command '{alias}' id '{cmdId}' not found"); continue; }
                                ids[alias] = cid;
                                items.Add(new CommandInfo
                                {
                                    Name = alias, Id = cid.Name, Kind = "addin", Words = alias.Replace('.', ' ').Replace('_', ' ').ToLowerInvariant(),
                                    DisplayName = o["displayName"]?.ToString() ?? alias, RibbonPath = o["ribbonPath"]?.ToString(),
                                    Interaction = o["interaction"]?.ToString() ?? "unknown"
                                });
                            }
                    }
                }
                catch (Exception ex) { RcdRuntime.Log("user commands load failed: " + ex.Message); }

                // 6) Tags + search blob
                foreach (var it in items)
                {
                    it.Tags = DeriveTags(it);
                    it.SearchText = string.Join(" ", new[] { it.Name, it.Id, it.Words, it.DisplayName, it.RibbonPath, string.Join(" ", it.Shortcuts), string.Join(" ", it.Tags) }
                        .Where(s => !string.IsNullOrEmpty(s))).ToLowerInvariant();
                }

                _items = items.OrderBy(i => i.Name, StringComparer.OrdinalIgnoreCase).ToList();
                _idsByName = ids;
                RcdRuntime.Log($"Catalog built: {items.Count} commands, shortcuts file={_shortcutsSource ?? "(none)"} matched={_shortcutsMatched}, {sw.ElapsedMilliseconds} ms");
            }
        }

        public static void Invalidate() { lock (_lock) { _items = null; _idsByName = null; } }

        public static List<CommandInfo> Search(string query, IList<string> tags, int limit)
        {
            lock (_lock)
            {
                if (_items == null) return new List<CommandInfo>();
                IEnumerable<CommandInfo> q = _items;
                if (tags != null && tags.Count > 0)
                {
                    var tl = tags.Select(t => t.ToLowerInvariant()).ToList();
                    q = q.Where(i => i.Tags.Any(t => tl.Contains(t)));
                }
                if (string.IsNullOrWhiteSpace(query))
                    return q.Take(limit).ToList();

                string ql = query.Trim().ToLowerInvariant();
                var tokens = ql.Split(new[] { ' ', ',', '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
                var scored = new List<(CommandInfo item, int score)>();
                foreach (var it in q)
                {
                    int score = 0;
                    if (it.Name.Equals(ql, StringComparison.OrdinalIgnoreCase) || it.Id.Equals(ql, StringComparison.OrdinalIgnoreCase)) score += 1000;
                    if (it.Shortcuts.Any(s => s.Equals(ql, StringComparison.OrdinalIgnoreCase))) score += 900;
                    if (it.Name.StartsWith(ql, StringComparison.OrdinalIgnoreCase)) score += 300;
                    if (it.Words.Split(' ').Any(w => w == ql)) score += 250;
                    int matched = 0;
                    foreach (var t in tokens)
                    {
                        if (it.SearchText.Contains(t)) { matched++; score += 50; }
                        if (it.Words.Split(' ').Any(w => w.StartsWith(t))) score += 30;
                    }
                    if (tokens.Length > 1 && matched == tokens.Length) score += 200;
                    if (matched == 0 && score == 0) continue;
                    if (it.Kind == "postable" && it.Interaction != "unknown") score += 5; // prefer curated commands on ties
                    scored.Add((it, score));
                }
                return scored.OrderByDescending(s => s.score).ThenBy(s => s.item.Name.Length).Select(s => s.item).Take(limit).ToList();
            }
        }

        /// <summary>
        /// Resolves a user-facing command reference (enum name, command id, shortcut, alias, or raw
        /// journal id) to a RevitCommandId. Throws DriverException COMMAND_NOT_FOUND / AMBIGUOUS_COMMAND.
        /// </summary>
        public static (RevitCommandId id, CommandInfo info) Resolve(string reference)
        {
            if (string.IsNullOrWhiteSpace(reference))
                throw new DriverException(RcdErrorCodes.InvalidArgument, "command is required (enum name like 'ArchitecturalWall', id like 'ID_OBJECTS_WALL', or shortcut like 'WA').");
            string r = reference.Trim();
            lock (_lock)
            {
                if (_items == null) throw new DriverException(RcdErrorCodes.InternalError, "Catalog not built yet.");

                var byName = _items.FirstOrDefault(i => i.Name.Equals(r, StringComparison.OrdinalIgnoreCase));
                if (byName != null) return (_idsByName[byName.Name], byName);

                var byId = _items.Where(i => i.Id.Equals(r, StringComparison.OrdinalIgnoreCase)).ToList();
                if (byId.Count == 1) return (_idsByName[byId[0].Name], byId[0]);
                if (byId.Count > 1) return (_idsByName[byId[0].Name], byId[0]); // same RevitCommandId under several enum names — any is fine

                var byShortcut = _items.Where(i => i.Shortcuts.Any(s => s.Equals(r, StringComparison.OrdinalIgnoreCase))).ToList();
                if (byShortcut.Count == 1) return (_idsByName[byShortcut[0].Name], byShortcut[0]);
                if (byShortcut.Count > 1)
                    throw new DriverException(RcdErrorCodes.AmbiguousCommand, $"Shortcut '{r}' maps to {byShortcut.Count} commands; use the enum name.",
                        new { candidates = byShortcut.Select(i => new { i.Name, i.Id, i.RibbonPath }).ToList() });

                // Raw journal / add-in id not in the catalog
                if (r.StartsWith("ID_", StringComparison.OrdinalIgnoreCase) || r.Contains("%"))
                {
                    RevitCommandId cid = null;
                    try { cid = RevitCommandId.LookupCommandId(r); } catch { }
                    if (cid != null)
                        return (cid, new CommandInfo { Name = r, Id = cid.Name, Kind = "raw", Interaction = "unknown" });
                }

                var fuzzy = Search(r, null, 6);
                throw new DriverException(RcdErrorCodes.CommandNotFound, $"No Revit command matches '{r}'." + (fuzzy.Count > 0 ? " Did you mean one of the candidates?" : ""),
                    new { candidates = fuzzy.Select(i => new { i.Name, i.Id, i.Shortcuts, i.Interaction }).ToList() });
            }
        }

        // ── helpers ─────────────────────────────────────────────────────────

        private static string SplitCamel(string s, bool titleCase = false)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (i > 0 && char.IsUpper(c) && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]) && char.IsUpper(s[i - 1]))))
                    sb.Append(' ');
                else if (i > 0 && char.IsDigit(c) && !char.IsDigit(s[i - 1])) sb.Append(' ');
                sb.Append(titleCase ? c : char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }

        private static readonly Regex TagSplit = new Regex(@"[>:]", RegexOptions.Compiled);

        private static List<string> DeriveTags(CommandInfo it)
        {
            var tags = new List<string>();
            if (!string.IsNullOrEmpty(it.RibbonPath))
            {
                var parts = TagSplit.Split(it.RibbonPath).Select(p => p.Trim().ToLowerInvariant()).Where(p => p.Length > 0).ToList();
                foreach (var p in parts.Take(2)) if (!tags.Contains(p)) tags.Add(p);
            }
            string w = it.Words ?? string.Empty;
            void tag(string t) { if (!tags.Contains(t)) tags.Add(t); }
            if (w.Contains("wall") || w.Contains("door") || w.Contains("window") || w.Contains("floor") || w.Contains("roof") || w.Contains("ceiling") || w.Contains("column") || w.Contains("stair") || w.Contains("ramp") || w.Contains("railing") || w.Contains("room") || w.Contains("curtain")) tag("architecture");
            if (w.Contains("beam") || w.Contains("brace") || w.Contains("truss") || w.Contains("foundation") || w.Contains("rebar") || w.Contains("structural")) tag("structure");
            if (w.Contains("dimension") || w.Contains("tag") || w.Contains("text") || w.Contains("keynote") || w.Contains("spot") || w.Contains("detail") || w.Contains("symbol") || w.Contains("revision")) tag("annotate");
            if (w.Contains("duct") || w.Contains("pipe") || w.Contains("cable") || w.Contains("conduit") || w.Contains("mechanical") || w.Contains("electrical") || w.Contains("plumbing") || w.Contains("sprinkler")) tag("mep");
            if (w.Contains("view") || w.Contains("zoom") || w.Contains("section") || w.Contains("elevation") || w.Contains("plan") || w.Contains("sheet") || w.Contains("schedule") || w.Contains("hide") || w.Contains("isolate") || w.Contains("visibility") || w.Contains("window")) tag("view");
            if (w.Contains("copy") || w.Contains("move") || w.Contains("rotate") || w.Contains("mirror") || w.Contains("array") || w.Contains("align") || w.Contains("trim") || w.Contains("split") || w.Contains("offset") || w.Contains("pin") || w.Contains("delete") || w.Contains("join") || w.Contains("scale") || w.Contains("paint") || w.Contains("group")) tag("modify");
            if (w.Contains("save") || w.Contains("synchronize") || w.Contains("print") || w.Contains("export") || w.Contains("import") || w.Contains("link") || w.Contains("open") || w.Contains("close") || w.Contains("purge") || w.Contains("audit") || w.Contains("publish")) tag("file");
            if (w.Contains("level") || w.Contains("grid") || w.Contains("reference plane")) tag("datum");
            if (it.Kind == "addin") tag("addin");
            return tags;
        }
    }
}
