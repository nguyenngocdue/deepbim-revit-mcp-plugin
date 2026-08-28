using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Records every DocumentChanged event into a thread-safe ring buffer so socket-thread
    /// commands can answer "what did the last posted command create?" without an API context.
    /// </summary>
    public static class ChangeTracker
    {
        private const int Capacity = 2000;
        private static readonly object _lock = new object();
        private static readonly LinkedList<ChangeEntry> _entries = new LinkedList<ChangeEntry>();
        private static long _seq;
        private static bool _subscribed;

        public static long CurrentSeq { get { lock (_lock) return _seq; } }

        /// <summary>Returns the current sequence number to be used as a marker.</summary>
        public static long Mark() => CurrentSeq;

        public static void Subscribe(UIApplication uiapp)
        {
            lock (_lock)
            {
                if (_subscribed) return;
                uiapp.Application.DocumentChanged += OnDocumentChanged;
                _subscribed = true;
            }
        }

        private static void OnDocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                var entry = new ChangeEntry
                {
                    Utc = DateTime.UtcNow,
                    Operation = e.Operation.ToString(),
                    TransactionNames = e.GetTransactionNames()?.ToArray() ?? Array.Empty<string>(),
                    Added = e.GetAddedElementIds().Select(id => id.GetValue()).ToArray(),
                    Modified = e.GetModifiedElementIds().Select(id => id.GetValue()).ToArray(),
                    Deleted = e.GetDeletedElementIds().Select(id => id.GetValue()).ToArray()
                };
                try { entry.DocTitle = e.GetDocument()?.Title; } catch { }

                lock (_lock)
                {
                    entry.Seq = ++_seq;
                    _entries.AddLast(entry);
                    while (_entries.Count > Capacity) _entries.RemoveFirst();
                }

                RcdRuntime.LogVerbose($"DocumentChanged seq={entry.Seq} op={entry.Operation} txn=[{string.Join(",", entry.TransactionNames)}] +{entry.Added.Length} ~{entry.Modified.Length} -{entry.Deleted.Length}");
            }
            catch (Exception ex)
            {
                RcdRuntime.Log("ChangeTracker.OnDocumentChanged failed: " + ex.Message);
            }
        }

        /// <summary>Aggregates all entries with Seq &gt; <paramref name="sinceSeq"/>.</summary>
        public static ChangeSet Since(long sinceSeq, int maxIds = 500)
        {
            var set = new ChangeSet { FromSeq = sinceSeq };
            lock (_lock)
            {
                set.ToSeq = _seq;
                foreach (var e in _entries)
                {
                    if (e.Seq <= sinceSeq) continue;
                    set.Entries++;
                    set.Operations.Add(e.Operation);
                    foreach (var t in e.TransactionNames) set.TransactionNames.Add(t);
                    AddCapped(set.Added, e.Added, maxIds, set);
                    AddCapped(set.Modified, e.Modified, maxIds, set);
                    AddCapped(set.Deleted, e.Deleted, maxIds, set);
                }
            }
            // Something added then deleted in the same window is noise for the AI — drop it from Added.
            if (set.Deleted.Count > 0 && set.Added.Count > 0)
            {
                var deleted = new HashSet<long>(set.Deleted);
                set.Added.RemoveAll(deleted.Contains);
            }
            // Modified often echoes Added; keep Modified strictly "not newly added".
            if (set.Added.Count > 0 && set.Modified.Count > 0)
            {
                var added = new HashSet<long>(set.Added);
                set.Modified.RemoveAll(added.Contains);
            }
            set.Modified = set.Modified.Distinct().ToList();
            return set;
        }

        private static void AddCapped(List<long> target, long[] source, int max, ChangeSet set)
        {
            foreach (var id in source)
            {
                if (target.Count >= max) { set.Truncated = true; return; }
                target.Add(id);
            }
        }
    }
}
