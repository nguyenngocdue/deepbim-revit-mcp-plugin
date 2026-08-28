using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace RevitMCPCommandSet.Driver.Models
{
    /// <summary>Well-known RCD error codes (see docs/revit-command-driver/01-spec.md §8).</summary>
    public static class RcdErrorCodes
    {
        public const string DriverDisabled = "DRIVER_DISABLED";
        public const string DriverBusy = "DRIVER_BUSY";
        public const string CommandNotFound = "COMMAND_NOT_FOUND";
        public const string AmbiguousCommand = "AMBIGUOUS_COMMAND";
        public const string CannotPost = "CANNOT_POST";
        public const string PostPending = "POST_PENDING";
        public const string ExternalEventTimeout = "EXTERNAL_EVENT_TIMEOUT";
        public const string ViewNot2D = "VIEW_NOT_2D";
        public const string NoMapping = "NO_MAPPING";
        public const string PointOffScreen = "POINT_OFF_SCREEN";
        public const string ForegroundFailed = "FOREGROUND_FAILED";
        public const string ForegroundLost = "FOREGROUND_LOST";
        public const string StatusTimeout = "STATUS_TIMEOUT";
        public const string StatusMismatch = "STATUS_MISMATCH";
        public const string DialogOpen = "DIALOG_OPEN";
        public const string UserAborted = "USER_ABORTED";
        public const string InvalidStep = "INVALID_STEP";
        public const string InvalidArgument = "INVALID_ARGUMENT";
        public const string InternalError = "INTERNAL_ERROR";
    }

    /// <summary>Exception carrying an RCD error code plus optional structured data for the AI.</summary>
    public class DriverException : Exception
    {
        public string Code { get; }
        public object Data2 { get; }

        public DriverException(string code, string message, object data = null) : base(message)
        {
            Code = code;
            Data2 = data;
        }
    }

    public class CommandInfo
    {
        [JsonProperty("name")] public string Name { get; set; }
        [JsonProperty("id")] public string Id { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; } = "postable";
        [JsonProperty("shortcuts")] public List<string> Shortcuts { get; set; } = new List<string>();
        [JsonProperty("ribbonPath")] public string RibbonPath { get; set; }
        [JsonProperty("displayName")] public string DisplayName { get; set; }
        [JsonProperty("tags")] public List<string> Tags { get; set; } = new List<string>();
        [JsonProperty("interaction")] public string Interaction { get; set; } = "unknown";
        [JsonProperty("canPost", NullValueHandling = NullValueHandling.Ignore)] public bool? CanPost { get; set; }

        /// <summary>Lower-cased search blob built once at catalog build time.</summary>
        [JsonIgnore] public string SearchText { get; set; }
        /// <summary>Human words derived from the CamelCase enum name ("ArchitecturalWall" → "architectural wall").</summary>
        [JsonIgnore] public string Words { get; set; }
    }

    /// <summary>Affine mapping between the active 2D view's model plane and screen pixels.</summary>
    public class ViewMapping
    {
        [JsonProperty("viewId")] public long ViewId { get; set; }
        [JsonProperty("viewName")] public string ViewName { get; set; }
        [JsonProperty("viewType")] public string ViewType { get; set; }
        [JsonProperty("screenRect")] public int[] ScreenRect { get; set; }          // [left, top, right, bottom] px
        [JsonProperty("modelCornersMm")] public double[][] ModelCornersMm { get; set; } // [[x,y,z],[x,y,z]]
        [JsonProperty("mmPerPixel")] public double MmPerPixel { get; set; }
        [JsonProperty("dpiScale")] public double DpiScale { get; set; }
        [JsonProperty("capturedUtc")] public DateTime CapturedUtc { get; set; }
        [JsonProperty("capturedAtSeq")] public long CapturedAtSeq { get; set; }
        [JsonProperty("mainWindowRectWin32")] public int[] MainWindowRectWin32 { get; set; }

        // Internal projection data (feet, view basis) — not serialized.
        [JsonIgnore] public double OriginX, OriginY, OriginZ;
        [JsonIgnore] public double RightX, RightY, RightZ;
        [JsonIgnore] public double UpX, UpY, UpZ;
        [JsonIgnore] public double UMin, UMax, VMin, VMax;

        [JsonIgnore] public int Left => ScreenRect[0];
        [JsonIgnore] public int Top => ScreenRect[1];
        [JsonIgnore] public int Right => ScreenRect[2];
        [JsonIgnore] public int Bottom => ScreenRect[3];
        [JsonIgnore] public int Width => Right - Left;
        [JsonIgnore] public int Height => Bottom - Top;

        /// <summary>Project a model point (mm) onto the screen. Returns fractional pixels.</summary>
        public (double px, double py) ToScreen(double xMm, double yMm, double zMm)
        {
            double xf = xMm / 304.8, yf = yMm / 304.8, zf = zMm / 304.8;
            double dx = xf - OriginX, dy = yf - OriginY, dz = zf - OriginZ;
            double u = dx * RightX + dy * RightY + dz * RightZ;
            double v = dx * UpX + dy * UpY + dz * UpZ;
            double px = Left + (u - UMin) / (UMax - UMin) * Width;
            double py = Bottom - (v - VMin) / (VMax - VMin) * Height;
            return (px, py);
        }

        public bool IsOnScreen(double px, double py, int margin = 4)
            => px >= Left + margin && px <= Right - margin && py >= Top + margin && py <= Bottom - margin;
    }

    public class ChangeEntry
    {
        public long Seq;
        public DateTime Utc;
        public string DocTitle;
        public string Operation;
        public string[] TransactionNames;
        public long[] Added;
        public long[] Modified;
        public long[] Deleted;
    }

    public class ChangeSet
    {
        [JsonProperty("fromSeq")] public long FromSeq { get; set; }
        [JsonProperty("toSeq")] public long ToSeq { get; set; }
        [JsonProperty("entries")] public int Entries { get; set; }
        [JsonProperty("transactionNames")] public List<string> TransactionNames { get; set; } = new List<string>();
        [JsonProperty("operations")] public List<string> Operations { get; set; } = new List<string>();
        [JsonProperty("added")] public List<long> Added { get; set; } = new List<long>();
        [JsonProperty("modified")] public List<long> Modified { get; set; } = new List<long>();
        [JsonProperty("deleted")] public List<long> Deleted { get; set; } = new List<long>();
        [JsonProperty("truncated")] public bool Truncated { get; set; }
    }

    public class DialogEvent
    {
        [JsonProperty("utc")] public DateTime Utc { get; set; }
        [JsonProperty("kind")] public string Kind { get; set; }
        [JsonProperty("dialogId")] public string DialogId { get; set; }
        [JsonProperty("message")] public string Message { get; set; }
        [JsonProperty("action")] public string Action { get; set; }
    }

    public class DialogRule
    {
        [JsonProperty("dialogId")] public string DialogId { get; set; }
        [JsonProperty("titleRegex")] public string TitleRegex { get; set; }
        [JsonProperty("messageRegex")] public string MessageRegex { get; set; }
        [JsonProperty("overrideResult")] public JToken OverrideResult { get; set; }
        [JsonProperty("once")] public bool Once { get; set; }
        [JsonProperty("expiresUtc")] public DateTime? ExpiresUtc { get; set; }
        [JsonIgnore] public bool Consumed { get; set; }
    }

    public class DialogInfo
    {
        [JsonProperty("hwnd")] public long Hwnd { get; set; }
        [JsonProperty("className")] public string ClassName { get; set; }
        [JsonProperty("title")] public string Title { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("buttons")] public List<string> Buttons { get; set; } = new List<string>();
    }

    /// <summary>One step in an rcd_ui_input batch. Discriminated by <see cref="Type"/>.</summary>
    public class InputStep
    {
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("point")] public double[] Point { get; set; }        // [x,y,z] mm
        [JsonProperty("screen")] public double[] Screen { get; set; }      // [px,py]
        [JsonProperty("from")] public double[] From { get; set; }
        [JsonProperty("to")] public double[] To { get; set; }
        [JsonProperty("button")] public string Button { get; set; } = "left";
        [JsonProperty("holdShift")] public bool HoldShift { get; set; }
        [JsonProperty("snapOverride")] public string SnapOverride { get; set; }
        [JsonProperty("text")] public string Text { get; set; }
        [JsonProperty("enter")] public bool Enter { get; set; }
        [JsonProperty("key")] public string Key { get; set; }
        [JsonProperty("times")] public int Times { get; set; } = 1;
        [JsonProperty("modifiers")] public List<string> Modifiers { get; set; }
        [JsonProperty("ms")] public int Ms { get; set; }
        [JsonProperty("contains")] public string Contains { get; set; }
        [JsonProperty("regex")] public string Regex { get; set; }
        [JsonProperty("timeoutMs")] public int TimeoutMs { get; set; }
        [JsonProperty("minAdded")] public int MinAdded { get; set; } = 1;
        [JsonProperty("expectStatus")] public string ExpectStatus { get; set; }
    }

    public class InputStepResult
    {
        [JsonProperty("index")] public int Index { get; set; }
        [JsonProperty("type")] public string Type { get; set; }
        [JsonProperty("ok")] public bool Ok { get; set; }
        [JsonProperty("screen", NullValueHandling = NullValueHandling.Ignore)] public int[] Screen { get; set; }
        [JsonProperty("statusAfter", NullValueHandling = NullValueHandling.Ignore)] public string StatusAfter { get; set; }
        [JsonProperty("elapsedMs")] public long ElapsedMs { get; set; }
        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)] public string Error { get; set; }
        [JsonProperty("errorCode", NullValueHandling = NullValueHandling.Ignore)] public string ErrorCode { get; set; }
    }
}
