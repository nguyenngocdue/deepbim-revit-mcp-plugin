using RevitMCPCommandSet.Driver.Models;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// One driver session at a time. Holds the last captured mapping/marker so rcd_ui_input can run
    /// on the socket thread without touching the Revit API. Tokens are cooperative: a missing token
    /// means "default" so a single agent never has to manage them.
    /// </summary>
    public static class DriverLock
    {
        private static readonly object _lock = new object();
        private static string _owner;
        private static DateTime _expiresUtc;
        private static volatile bool _abort;

        public static ViewMapping Mapping { get; private set; }
        public static long Marker { get; private set; }
        public static string LastCommand { get; private set; }
        public static DateTime LastPostedUtc { get; private set; }

        public static string Normalize(string token) => string.IsNullOrWhiteSpace(token) ? "default" : token.Trim();

        public static void Acquire(string token, int ttlMs)
        {
            token = Normalize(token);
            lock (_lock)
            {
                if (_owner != null && _owner != token && DateTime.UtcNow < _expiresUtc)
                    throw new DriverException(RcdErrorCodes.DriverBusy, $"Driver is locked by session '{_owner}' until {_expiresUtc:HH:mm:ss}Z. Call rcd_ui_cancel with that token or wait.",
                        new { ownerToken = _owner, expiresUtc = _expiresUtc });
                _owner = token;
                _expiresUtc = DateTime.UtcNow.AddMilliseconds(ttlMs <= 0 ? 120000 : ttlMs);
                _abort = false;
            }
        }

        public static void AssertOwnerOrFree(string token)
        {
            token = Normalize(token);
            lock (_lock)
            {
                if (_owner != null && _owner != token && DateTime.UtcNow < _expiresUtc)
                    throw new DriverException(RcdErrorCodes.DriverBusy, $"Driver is locked by session '{_owner}'.", new { ownerToken = _owner, expiresUtc = _expiresUtc });
            }
        }

        public static void Touch(int ttlMs)
        {
            lock (_lock) { if (_owner != null) _expiresUtc = DateTime.UtcNow.AddMilliseconds(ttlMs <= 0 ? 120000 : ttlMs); }
        }

        public static void Release(string token, bool force = false)
        {
            token = Normalize(token);
            lock (_lock)
            {
                if (force || _owner == null || _owner == token || DateTime.UtcNow >= _expiresUtc)
                {
                    _owner = null;
                    _abort = false;
                }
            }
        }

        public static void RequestAbort() { _abort = true; }
        public static bool AbortRequested => _abort;
        public static void ClearAbort() { _abort = false; }

        public static void SetSession(ViewMapping mapping, long marker, string command)
        {
            lock (_lock)
            {
                Mapping = mapping;
                Marker = marker;
                LastCommand = command;
                LastPostedUtc = DateTime.UtcNow;
            }
        }

        public static object Snapshot()
        {
            lock (_lock)
            {
                bool locked = _owner != null && DateTime.UtcNow < _expiresUtc;
                return new
                {
                    locked,
                    ownerToken = locked ? _owner : null,
                    expiresUtc = locked ? (DateTime?)_expiresUtc : null,
                    abortRequested = _abort,
                    lastCommand = LastCommand,
                    lastPostedUtc = LastPostedUtc == default ? (DateTime?)null : LastPostedUtc,
                    marker = Marker,
                    hasMapping = Mapping != null,
                    mappingView = Mapping?.ViewName,
                    mappingAgeSec = Mapping == null ? (double?)null : Math.Round((DateTime.UtcNow - Mapping.CapturedUtc).TotalSeconds, 1)
                };
            }
        }
    }
}
