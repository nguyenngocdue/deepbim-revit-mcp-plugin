using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RevitMCPCommandSet.Models.Common
{
    /// <summary>
    /// Filter settings — supports combined condition filtering.
    /// </summary>
    public class FilterSetting
    {
        /// <summary>
        /// Revit built-in category name to filter (e.g. "OST_Walls"). Null or empty = no category filter.
        /// </summary>
        [JsonProperty("filterCategory")]
        public string FilterCategory { get; set; } = null;
        /// <summary>
        /// Revit element type name to filter (e.g. "Wall"). Null or empty = no type filter.
        /// </summary>
        [JsonProperty("filterElementType")]
        public string FilterElementType { get; set; } = null;
        /// <summary>
        /// FamilySymbol ElementId to filter. 0 or negative = no family filter. Applies to instances only.
        /// </summary>
        [JsonProperty("filterFamilySymbolId")]
        public int FilterFamilySymbolId { get; set; } = -1;
        /// <summary>
        /// Include element types (e.g. wall type, door type).
        /// </summary>
        [JsonProperty("includeTypes")]
        public bool IncludeTypes { get; set; } = false;
        /// <summary>
        /// Include element instances (e.g. placed walls, doors).
        /// </summary>
        [JsonProperty("includeInstances")]
        public bool IncludeInstances { get; set; } = true;
        /// <summary>
        /// Only return elements visible in the current view. Applies to instances only.
        /// </summary>
        [JsonProperty("filterVisibleInCurrentView")]
        public bool FilterVisibleInCurrentView { get; set; }
        /// <summary>
        /// Bounding box minimum (mm). With BoundingBoxMax, filters elements intersecting the box.
        /// </summary>
        [JsonProperty("boundingBoxMin")]
        public JZPoint BoundingBoxMin { get; set; } = null;
        /// <summary>
        /// Bounding box maximum (mm). With BoundingBoxMin, filters elements intersecting the box.
        /// </summary>
        [JsonProperty("boundingBoxMax")]
        public JZPoint BoundingBoxMax { get; set; } = null;
        /// <summary>
        /// Maximum number of elements to return.
        /// </summary>
        [JsonProperty("maxElements")]
        public int MaxElements { get; set; } = 50; 
        /// <summary>
        /// Validates filter settings and checks for conflicts.
        /// </summary>
        /// <returns>True if valid.</returns>
        public bool Validate(out string errorMessage)
        {
            errorMessage = null;

            if (!IncludeTypes && !IncludeInstances)
            {
                errorMessage = "Filter settings invalid: must include at least element types or instances.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(FilterCategory) &&
                string.IsNullOrWhiteSpace(FilterElementType) &&
                FilterFamilySymbolId <= 0)
            {
                errorMessage = "Filter settings invalid: must specify at least one filter (category, element type, or family type).";
                return false;
            }

            if (IncludeTypes && !IncludeInstances)
            {
                List<string> invalidFilters = new List<string>();
                if (FilterFamilySymbolId > 0)
                    invalidFilters.Add("Family instance filter");
                if (FilterVisibleInCurrentView)
                    invalidFilters.Add("View visibility filter");
                if (invalidFilters.Count > 0)
                {
                    errorMessage = $"When filtering type elements only, these filters do not apply: {string.Join(", ", invalidFilters)}";
                    return false;
                }
            }
            if (BoundingBoxMin != null && BoundingBoxMax != null)
            {
                if (BoundingBoxMin.X > BoundingBoxMax.X ||
                    BoundingBoxMin.Y > BoundingBoxMax.Y ||
                    BoundingBoxMin.Z > BoundingBoxMax.Z)
                {
                    errorMessage = "Bounding box invalid: min must be less than or equal to max.";
                    return false;
                }
            }
            else if (BoundingBoxMin != null || BoundingBoxMax != null)
            {
                errorMessage = "Bounding box invalid: both min and max must be set.";
                return false;
            }
            return true;
        }
    }
}
