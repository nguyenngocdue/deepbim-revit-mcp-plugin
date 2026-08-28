using System.IO;
using System.Xml.Linq;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Parses Revit's KeyboardShortcuts.xml (user export / customized file) to enrich the command
    /// catalog with shortcuts and ribbon paths. Everything is best-effort: missing file → empty map.
    /// </summary>
    public static class KeyboardShortcutsReader
    {
        public class Entry
        {
            public string CommandId;
            public string CommandName;
            public string Paths;
            public List<string> Shortcuts = new List<string>();
        }

        public static IEnumerable<string> CandidatePaths(string revitVersion)
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            yield return Path.Combine(RcdRuntime.RcdDir, "KeyboardShortcuts.xml");            // explicit user-provided copy
            yield return Path.Combine(appData, "Autodesk", "Revit", $"Autodesk Revit {revitVersion}", "KeyboardShortcuts.xml");
            yield return Path.Combine(appData, "Autodesk", "Revit", $"Autodesk Revit {revitVersion}", "ENU", "KeyboardShortcuts.xml");
            yield return Path.Combine(programData, "Autodesk", $"RVT {revitVersion}", "UserDataCache", "KeyboardShortcuts.xml");
        }

        public static string FindFile(string revitVersion)
            => CandidatePaths(revitVersion).FirstOrDefault(File.Exists);

        /// <summary>Returns entries keyed by CommandId (e.g. "ID_OBJECTS_WALL"). Empty when no file is found.</summary>
        public static Dictionary<string, Entry> Load(string revitVersion, out string sourcePath)
        {
            var map = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
            sourcePath = FindFile(revitVersion);
            if (sourcePath == null) return map;
            try
            {
                var doc = XDocument.Load(sourcePath);
                foreach (var item in doc.Descendants("ShortcutItem"))
                {
                    string id = (string)item.Attribute("CommandId");
                    if (string.IsNullOrEmpty(id)) continue;
                    if (!map.TryGetValue(id, out var e))
                    {
                        e = new Entry { CommandId = id, CommandName = (string)item.Attribute("CommandName"), Paths = (string)item.Attribute("Paths") };
                        map[id] = e;
                    }
                    string sc = (string)item.Attribute("Shortcuts");
                    if (!string.IsNullOrWhiteSpace(sc))
                        foreach (var s in sc.Split('#', ';', ','))
                            if (!string.IsNullOrWhiteSpace(s) && !e.Shortcuts.Contains(s.Trim(), StringComparer.OrdinalIgnoreCase))
                                e.Shortcuts.Add(s.Trim());
                }
            }
            catch (Exception ex)
            {
                RcdRuntime.Log($"KeyboardShortcutsReader: failed to parse {sourcePath}: {ex.Message}");
            }
            return map;
        }
    }
}
