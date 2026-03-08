using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Element operation types.
    /// </summary>
    public enum ElementOperationType
    {
        /// <summary>Select elements.</summary>
        Select,

        /// <summary>Selection box.</summary>
        SelectionBox,

        /// <summary>Set element color and fill.</summary>
        SetColor,

        /// <summary>Set element transparency.</summary>
        SetTransparency,

        /// <summary>Delete elements.</summary>
        Delete,

        /// <summary>Hide elements.</summary>
        Hide,

        /// <summary>Temporarily hide elements.</summary>
        TempHide,

        /// <summary>Isolate elements.</summary>
        Isolate,

        /// <summary>Unhide elements.</summary>
        Unhide,

        /// <summary>Reset isolate (show all).</summary>
        ResetIsolate,
    }

    /// <summary>
    /// Settings for element operations.
    /// </summary>
    public class OperationSetting
    {
        /// <summary>Element IDs to operate on.</summary>
        [JsonProperty("elementIds")]
        public List<int> ElementIds = new List<int>();

        /// <summary>Action to perform (ElementOperationType as string).</summary>
        [JsonProperty("action")]
        public string Action { get; set; } = "Select";

        /// <summary>Transparency 0-100 (higher = more transparent).</summary>
        [JsonProperty("transparencyValue")]
        public int TransparencyValue { get; set; } = 50;

        /// <summary>Element color (RGB). Default red.</summary>
        [JsonProperty("colorValue")]
        public int[] ColorValue { get; set; } = new int[] { 255, 0, 0 };
    }
}
