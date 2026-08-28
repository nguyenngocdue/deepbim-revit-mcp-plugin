using Autodesk.Revit.UI;
using RevitMCPCommandSet.Driver.Models;
using RevitMCPCommandSet.Driver.Native;
using RevitMCPCommandSet.Utils;

namespace RevitMCPCommandSet.Driver
{
    /// <summary>
    /// Builds the model↔screen affine mapping for a 2D view from UIView.GetWindowRectangle() and
    /// UIView.GetZoomCorners(). Must be called inside a Revit API context.
    /// </summary>
    public static class ScreenMapper
    {
        private static readonly HashSet<ViewType> Supported2D = new HashSet<ViewType>
        {
            ViewType.FloorPlan, ViewType.CeilingPlan, ViewType.EngineeringPlan, ViewType.AreaPlan,
            ViewType.Section, ViewType.Elevation, ViewType.Detail, ViewType.DraftingView, ViewType.Legend
        };

        public static bool Is2D(View view) => view != null && Supported2D.Contains(view.ViewType);

        public static UIView FindUIView(UIDocument uidoc, View view)
        {
            foreach (var uv in uidoc.GetOpenUIViews())
                if (uv.ViewId == view.Id) return uv;
            return null;
        }

        /// <summary>Zooms so that all points (mm) are visible with padding, then captures the mapping.</summary>
        public static ViewMapping FitAndCapture(UIDocument uidoc, View view, IList<double[]> pointsMm, double paddingMm, double maxMmPerPixel, List<string> warnings)
        {
            var uiview = FindUIView(uidoc, view) ?? throw new DriverException(RcdErrorCodes.ViewNot2D, $"View '{view.Name}' is not open in a window.");

            if (pointsMm != null && pointsMm.Count > 0)
            {
                var pts = pointsMm.Select(p => new XYZ(p[0] / 304.8, p[1] / 304.8, (p.Length > 2 ? p[2] : 0) / 304.8)).ToList();
                double pad = Math.Max(paddingMm, 100) / 304.8;

                // Work in the view's own basis so sections/elevations fit correctly.
                var origin = view.Origin; var right = view.RightDirection; var up = view.UpDirection;
                double uMin = double.MaxValue, uMax = double.MinValue, vMin = double.MaxValue, vMax = double.MinValue;
                foreach (var p in pts)
                {
                    var d = p - origin;
                    double u = d.DotProduct(right), v = d.DotProduct(up);
                    uMin = Math.Min(uMin, u); uMax = Math.Max(uMax, u);
                    vMin = Math.Min(vMin, v); vMax = Math.Max(vMax, v);
                }
                // Degenerate (single point / collinear) → give it a minimum extent.
                double minExtent = 1000 / 304.8;
                if (uMax - uMin < minExtent) { double c = (uMin + uMax) / 2; uMin = c - minExtent / 2; uMax = c + minExtent / 2; }
                if (vMax - vMin < minExtent) { double c = (vMin + vMax) / 2; vMin = c - minExtent / 2; vMax = c + minExtent / 2; }
                uMin -= pad; uMax += pad; vMin -= pad; vMax += pad;

                // Keep the requested precision if possible: the fitted rectangle will be stretched by
                // Revit to the window aspect ratio, so the effective mm/px is governed by the larger ratio.
                var rectNow = uiview.GetWindowRectangle();
                int w = Math.Max(1, rectNow.Right - rectNow.Left), h = Math.Max(1, rectNow.Bottom - rectNow.Top);
                double mmPerPx = Math.Max((uMax - uMin) * 304.8 / w, (vMax - vMin) * 304.8 / h);
                if (maxMmPerPixel > 0 && mmPerPx > maxMmPerPixel)
                    warnings?.Add($"Requested points need {mmPerPx:F1} mm/px to fit; exceeds maxMmPerPixel {maxMmPerPixel}. Clicks will be less precise — rely on snapping (snapOverride) or split into smaller fits.");

                var c1 = origin + right * uMin + up * vMin;
                var c2 = origin + right * uMax + up * vMax;
                uiview.ZoomAndCenterRectangle(c1, c2);
            }

            return Capture(uidoc, view, uiview);
        }

        public static ViewMapping Capture(UIDocument uidoc, View view, UIView uiview = null)
        {
            if (!Is2D(view))
                throw new DriverException(RcdErrorCodes.ViewNot2D, $"Active view '{view.Name}' is {view.ViewType}; RCD mapping supports plan/section/elevation/drafting/legend views only.",
                    new { activeViewType = view.ViewType.ToString() });

            uiview ??= FindUIView(uidoc, view) ?? throw new DriverException(RcdErrorCodes.ViewNot2D, $"View '{view.Name}' is not open in a window.");

            var rect = uiview.GetWindowRectangle();
            var corners = uiview.GetZoomCorners();
            if (corners == null || corners.Count < 2) throw new DriverException(RcdErrorCodes.NoMapping, "UIView.GetZoomCorners returned no corners.");

            var origin = view.Origin; var right = view.RightDirection; var up = view.UpDirection;
            var m = new ViewMapping
            {
                ViewId = view.Id.GetValue(),
                ViewName = view.Name,
                ViewType = view.ViewType.ToString(),
                ScreenRect = new[] { rect.Left, rect.Top, rect.Right, rect.Bottom },
                ModelCornersMm = corners.Take(2).Select(c => new[] { c.X * 304.8, c.Y * 304.8, c.Z * 304.8 }).ToArray(),
                CapturedUtc = DateTime.UtcNow,
                CapturedAtSeq = ChangeTracker.CurrentSeq,
                OriginX = origin.X, OriginY = origin.Y, OriginZ = origin.Z,
                RightX = right.X, RightY = right.Y, RightZ = right.Z,
                UpX = up.X, UpY = up.Y, UpZ = up.Z,
                DpiScale = Win32.GetDpiScaleForWindow(RcdRuntime.MainHwnd),
                MainWindowRectWin32 = WindowProbe.MainWindowRect()
            };

            double uA = (corners[0] - origin).DotProduct(right), vA = (corners[0] - origin).DotProduct(up);
            double uB = (corners[1] - origin).DotProduct(right), vB = (corners[1] - origin).DotProduct(up);
            m.UMin = Math.Min(uA, uB); m.UMax = Math.Max(uA, uB);
            m.VMin = Math.Min(vA, vB); m.VMax = Math.Max(vA, vB);
            if (m.UMax - m.UMin < 1e-9 || m.VMax - m.VMin < 1e-9 || m.Width <= 0 || m.Height <= 0)
                throw new DriverException(RcdErrorCodes.NoMapping, "Degenerate view mapping (zero-size zoom rectangle or window).");

            m.MmPerPixel = Math.Round(Math.Max((m.UMax - m.UMin) * 304.8 / m.Width, (m.VMax - m.VMin) * 304.8 / m.Height), 3);
            return m;
        }
    }
}
