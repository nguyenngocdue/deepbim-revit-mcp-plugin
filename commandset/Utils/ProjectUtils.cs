using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using RevitMCPCommandSet.Commands;
using RevitMCPCommandSet.Models.Common;
using System.IO;
using System.Reflection;

namespace RevitMCPCommandSet.Utils
{
    public static class ProjectUtils
    {
        /// <summary>
        /// Creates a family instance with the given placement type.
        /// </summary>
        /// <param name="doc">Current document.</param>
        /// <param name="familySymbol">Family type.</param>
        /// <param name="locationPoint">Location point.</param>
        /// <param name="locationLine">Base line.</param>
        /// <param name="baseLevel">Base level.</param>
        /// <param name="topLevel">Top level (for TwoLevelsBased).</param>
        /// <param name="baseOffset">Base offset (ft).</param>
        /// <param name="topOffset">Top offset (ft).</param>
        /// <param name="faceDirection">Face direction.</param>
        /// <param name="handDirection">Hand direction.</param>
        /// <param name="view">View.</param>
        /// <returns>Created family instance, or null on failure.</returns>
        public static FamilyInstance CreateInstance(
            this Document doc,
            FamilySymbol familySymbol,
            XYZ locationPoint = null,
            Line locationLine = null,
            Level baseLevel = null,
            Level topLevel = null,
            double baseOffset = -1,
            double topOffset = -1,
            XYZ faceDirection = null,
            XYZ handDirection = null,
            View view = null,
            Element explicitHost = null,
            bool snapToHostCenter = true)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), $"Required parameter {typeof(Document)} {nameof(doc)} is missing.");
            if (familySymbol == null)
                throw new ArgumentNullException(nameof(familySymbol), $"Required parameter {typeof(FamilySymbol)} {nameof(familySymbol)} is missing.");

            if (!familySymbol.IsActive)
                familySymbol.Activate();

            FamilyInstance instance = null;

            switch (familySymbol.Family.FamilyPlacementType)
            {
                case FamilyPlacementType.OneLevelBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException(nameof(locationPoint), $"Required parameter {typeof(XYZ)} {nameof(locationPoint)} is missing.");
                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,
                            familySymbol,
                            baseLevel,
                            StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationPoint,
                            familySymbol,
                            StructuralType.NonStructural);
                    }
                    break;

                case FamilyPlacementType.OneLevelBasedHosted:
                    if (locationPoint == null)
                        throw new ArgumentNullException(nameof(locationPoint), $"Required parameter {typeof(XYZ)} {nameof(locationPoint)} is missing.");

                    Element host = explicitHost;
                    XYZ placementPoint = locationPoint;

                    // If explicit host provided and it's a wall, snap to its centerline
                    if (host != null && snapToHostCenter && host is Wall explicitWall)
                    {
                        LocationCurve eLoc = explicitWall.Location as LocationCurve;
                        if (eLoc != null)
                        {
                            IntersectionResult eIr = eLoc.Curve.Project(locationPoint);
                            if (eIr != null)
                                placementPoint = new XYZ(eIr.XYZPoint.X, eIr.XYZPoint.Y, locationPoint.Z);
                        }
                    }

                    // Auto-detect host wall if not explicitly provided
                    if (host == null)
                    {
                        // Try geometric wall-centerline proximity first
                        var wallResult = doc.GetNearestWallByLocationLine(locationPoint, baseLevel);
                        if (wallResult.HasValue)
                        {
                            host = wallResult.Value.wall;
                            if (snapToHostCenter)
                                placementPoint = wallResult.Value.projectedPoint;
                        }
                        else
                        {
                            // Fall back to original ray-casting method
                            host = doc.GetNearestHostElement(locationPoint, familySymbol);
                        }
                    }

                    if (host == null)
                        throw new ArgumentNullException(nameof(host), "No valid host element found.");

                    if (baseLevel != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            baseLevel,
                            StructuralType.NonStructural);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            placementPoint,
                            familySymbol,
                            host,
                            StructuralType.NonStructural);
                    }

                    // Set sill height for windows (baseOffset maps to sill height for hosted elements)
                    if (instance != null && baseOffset != -1)
                    {
                        Parameter sillParam = instance.get_Parameter(BuiltInParameter.INSTANCE_SILL_HEIGHT_PARAM);
                        if (sillParam != null && !sillParam.IsReadOnly)
                        {
                            sillParam.Set(baseOffset);
                        }
                    }
                    break;

                case FamilyPlacementType.TwoLevelsBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException(nameof(locationPoint), $"Required parameter {typeof(XYZ)} {nameof(locationPoint)} is missing.");
                    if (baseLevel == null)
                        throw new ArgumentNullException(nameof(baseLevel), $"Required parameter {typeof(Level)} {nameof(baseLevel)} is missing.");
                    StructuralType structuralType = StructuralType.NonStructural;
                    if (familySymbol.Category.Id.GetIntValue() == (int)BuiltInCategory.OST_StructuralColumns)
                        structuralType = StructuralType.Column;
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,
                        familySymbol,
                        baseLevel,
                        structuralType);
                    if (instance != null)
                    {
                        if (baseLevel != null)
                        {
                            Parameter baseLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_PARAM);
                            if (baseLevelParam != null)
                                baseLevelParam.Set(baseLevel.Id);
                        }
                        if (topLevel != null)
                        {
                            Parameter topLevelParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_PARAM);
                            if (topLevelParam != null)
                                topLevelParam.Set(topLevel.Id);
                        }
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_BASE_LEVEL_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                        if (topOffset != -1)
                        {
                            Parameter topOffsetParam = instance.get_Parameter(BuiltInParameter.FAMILY_TOP_LEVEL_OFFSET_PARAM);
                            if (topOffsetParam != null && topOffsetParam.StorageType == StorageType.Double)
                            {
                                double topOffsetInternal = topOffset;
                                topOffsetParam.Set(topOffsetInternal);
                            }
                        }
                    }
                    break;

                case FamilyPlacementType.ViewBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException(nameof(locationPoint), $"Required parameter {typeof(XYZ)} {nameof(locationPoint)} is missing.");
                    instance = doc.Create.NewFamilyInstance(
                        locationPoint,
                        familySymbol,
                        view);
                    break;

                case FamilyPlacementType.WorkPlaneBased:
                    if (locationPoint == null)
                        throw new ArgumentNullException(nameof(locationPoint), $"Required parameter {typeof(XYZ)} {nameof(locationPoint)} is missing.");
                    Reference hostFace = doc.GetNearestFaceReference(locationPoint, 1000 / 304.8);
                    if (hostFace == null)
                        throw new ArgumentNullException(nameof(hostFace), "No valid host element found.");
                    if (faceDirection == null || faceDirection == XYZ.Zero)
                    {
                        var result = doc.GenerateDefaultOrientation(hostFace);
                        faceDirection = result.FacingOrientation;
                    }
                    instance = doc.Create.NewFamilyInstance(
                        hostFace,
                        locationPoint,
                        faceDirection,
                        familySymbol);
                    break;

                case FamilyPlacementType.CurveBased:
                    if (locationLine == null)
                        throw new ArgumentNullException(nameof(locationLine), $"Required parameter {typeof(Line)} {nameof(locationLine)} is missing.");

                    Reference lineHostFace = doc.GetNearestFaceReference(locationLine.Evaluate(0.5, true), 1e-5);
                    if (lineHostFace != null)
                    {
                        instance = doc.Create.NewFamilyInstance(
                            lineHostFace,
                            locationLine,
                            familySymbol);
                    }
                    else
                    {
                        instance = doc.Create.NewFamilyInstance(
                            locationLine,
                            familySymbol,
                            baseLevel,
                            StructuralType.NonStructural);
                    }
                    if (instance != null)
                    {
                        if (baseOffset != -1)
                        {
                            Parameter baseOffsetParam = instance.get_Parameter(BuiltInParameter.INSTANCE_FREE_HOST_OFFSET_PARAM);
                            if (baseOffsetParam != null && baseOffsetParam.StorageType == StorageType.Double)
                            {
                                double baseOffsetInternal = baseOffset;
                                baseOffsetParam.Set(baseOffsetInternal);
                            }
                        }
                    }
                    break;

                case FamilyPlacementType.CurveBasedDetail:
                    if (locationLine == null)
                        throw new ArgumentNullException(nameof(locationLine), $"Required parameter {typeof(Line)} {nameof(locationLine)} is missing.");
                    if (view == null)
                        throw new ArgumentNullException(nameof(view), $"Required parameter {typeof(View)} {nameof(view)} is missing.");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,
                        familySymbol,
                        view);
                    break;

                case FamilyPlacementType.CurveDrivenStructural:
                    if (locationLine == null)
                        throw new ArgumentNullException(nameof(locationLine), $"Required parameter {typeof(Line)} {nameof(locationLine)} is missing.");
                    if (baseLevel == null)
                        throw new ArgumentNullException(nameof(baseLevel), $"Required parameter {typeof(Level)} {nameof(baseLevel)} is missing.");
                    instance = doc.Create.NewFamilyInstance(
                        locationLine,
                        familySymbol,
                        baseLevel,
                        StructuralType.Beam);
                    break;

                case FamilyPlacementType.Adaptive:
                    throw new NotImplementedException("FamilyPlacementType.Adaptive creation is not implemented.");

                default:
                    break;
            }
            return instance;
        }

        /// <summary>
        /// Generates default facing and hand orientation (long edge = HandOrientation, short edge = FacingOrientation).
        /// </summary>
        /// <param name="hostFace"></param>
        /// <returns></returns>
        public static (XYZ FacingOrientation, XYZ HandOrientation) GenerateDefaultOrientation(this Document doc, Reference hostFace)
        {
            var facingOrientation = new XYZ();
            var handOrientation = new XYZ();

            Face face = doc.GetElement(hostFace.ElementId).GetGeometryObjectFromReference(hostFace) as Face;

            List<Curve> profile = null;
            List<List<Curve>> profiles = new List<List<Curve>>();
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            foreach (EdgeArray loop in edgeLoops)
            {
                List<Curve> currentLoop = new List<Curve>();
                // Get each edge in the loop
                foreach (Edge edge in loop)
                {
                    Curve curve = edge.AsCurve();
                    currentLoop.Add(curve);
                }
                // If current loop has edges, add to result
                if (currentLoop.Count > 0)
                {
                    profiles.Add(currentLoop);
                }
            }
            // First is usually the outer contour
            if (profiles != null && profiles.Any())
                profile = profiles.FirstOrDefault();

            // Get face normal
            XYZ faceNormal = null;
            // For planar face, use Normal directly
            if (face is PlanarFace planarFace)
                faceNormal = planarFace.FaceNormal;

            // Get two principal directions (right-hand rule)
            var result = face.GetMainDirections();
            var primaryDirection = result.PrimaryDirection;
            var secondaryDirection = result.SecondaryDirection;

            // Long edge = HandOrientation, short edge = FacingOrientation
            facingOrientation = primaryDirection;
            handOrientation = secondaryDirection;

            // Check right-hand rule (thumb: Hand, index: Facing, middle: FaceNormal)
            if (!facingOrientation.IsRightHandRuleCompliant(handOrientation, faceNormal))
            {
                var newHandOrientation = facingOrientation.GenerateIndexFinger(faceNormal);
                if (newHandOrientation != null)
                {
                    handOrientation = newHandOrientation;
                }
            }

            return (facingOrientation, handOrientation);
        }

        /// <summary>
        /// Gets the face Reference nearest to the given point.
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">目标点位置</param>
        /// <param name="radius">搜索半径（内部单位）</param>
        /// <returns>最近面的Reference，未找到返回null</returns>
        public static Reference GetNearestFaceReference(this Document doc, XYZ location, double radius = 1000 / 304.8)
        {
            try
            {
                // Tolerance
                location = new XYZ(location.X, location.Y, location.Z + 0.1 / 304.8);

                // Create or get 3D view
                View3D view3D = null;
                FilteredElementCollector collector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));

                foreach (View3D v in collector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("Error", "Could not create or get 3D view.");
                    return null;
                }

                // Rays in 6 directions
                XYZ[] directions = new XYZ[]
                {
                  XYZ.BasisX,    // X正向
                  -XYZ.BasisX,   // X负向
                  XYZ.BasisY,    // Y正向
                  -XYZ.BasisY,   // Y负向
                  XYZ.BasisZ,    // Z正向
                  -XYZ.BasisZ    // Z负向
                };

                // Create filter
                ElementClassFilter wallFilter = new ElementClassFilter(typeof(Wall));
                ElementClassFilter floorFilter = new ElementClassFilter(typeof(Floor));
                ElementClassFilter ceilingFilter = new ElementClassFilter(typeof(Ceiling));
                ElementClassFilter instanceFilter = new ElementClassFilter(typeof(FamilyInstance));

                // Combine filters
                LogicalOrFilter categoryFilter = new LogicalOrFilter(
                    new ElementFilter[] { wallFilter, floorFilter, ceilingFilter, instanceFilter });


                // 1. 最简单：所有实例化元素的过滤器
                //ElementFilter filter = new ElementIsElementTypeFilter(true);

                // 创建射线追踪器
                ReferenceIntersector refIntersector = new ReferenceIntersector(categoryFilter,
                    FindReferenceTarget.Face, view3D);
                    refIntersector.FindReferencesInRevitLinks = true;

                double minDistance = double.MaxValue;
                Reference nearestFace = null;

                foreach (XYZ direction in directions)
                {
                    // Cast ray from current position
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity;

                        // If within range and closer
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestFace = rwc.GetReference();
                        }
                    }
                }

                return nearestFace;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error getting nearest face: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Gets the nearest host-capable element to the point.
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="location">目标点位置</param>
        /// <param name="familySymbol">族类型，用于判断宿主类型</param>
        /// <param name="radius">搜索半径（内部单位）</param>
        /// <returns>最近的宿主元素，未找到返回null</returns>
        public static Element GetNearestHostElement(this Document doc, XYZ location, FamilySymbol familySymbol, double radius = 5.0)
        {
            try
            {
                // Parameter check
                if (doc == null || location == null || familySymbol == null)
                    return null;

                // Get family host behavior
                Parameter hostParam = familySymbol.Family.get_Parameter(BuiltInParameter.FAMILY_HOSTING_BEHAVIOR);
                int hostingBehavior = hostParam?.AsInteger() ?? 0;

                // Create or get 3D view
                View3D view3D = null;
                FilteredElementCollector viewCollector = new FilteredElementCollector(doc)
                    .OfClass(typeof(View3D));
                foreach (View3D v in viewCollector)
                {
                    if (!v.IsTemplate)
                    {
                        view3D = v;
                        break;
                    }
                }

                if (view3D == null)
                {
                    using (Transaction trans = new Transaction(doc, "Create 3D View"))
                    {
                        trans.Start();
                        ViewFamilyType vft = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewFamilyType))
                            .Cast<ViewFamilyType>()
                            .FirstOrDefault(x => x.ViewFamily == ViewFamily.ThreeDimensional);

                        if (vft != null)
                        {
                            view3D = View3D.CreateIsometric(doc, vft.Id);
                        }
                        trans.Commit();
                    }
                }

                if (view3D == null)
                {
                    TaskDialog.Show("Error", "Could not create or get 3D view.");
                    return null;
                }

                // 根据宿主行为创建类型过滤器
                ElementFilter classFilter;
                switch (hostingBehavior)
                {
                    case 1: // Wall based
                        classFilter = new ElementClassFilter(typeof(Wall));
                        break;
                    case 2: // Floor based
                        classFilter = new ElementClassFilter(typeof(Floor));
                        break;
                    case 3: // Ceiling based
                        classFilter = new ElementClassFilter(typeof(Ceiling));
                        break;
                    case 4: // Roof based
                        classFilter = new ElementClassFilter(typeof(RoofBase));
                        break;
                    default:
                        return null; // Unsupported host type
                }

                // Rays in 6 directions
                XYZ[] directions = new XYZ[]
                {
                    XYZ.BasisX,    // X正向
                    -XYZ.BasisX,   // X负向
                    XYZ.BasisY,    // Y正向
                    -XYZ.BasisY,   // Y负向
                    XYZ.BasisZ,    // Z正向
                    -XYZ.BasisZ    // Z负向
                };

                // 创建射线追踪器
                ReferenceIntersector refIntersector = new ReferenceIntersector(classFilter,
                    FindReferenceTarget.Element, view3D);
                refIntersector.FindReferencesInRevitLinks = true;

                double minDistance = double.MaxValue;
                Element nearestHost = null;

                foreach (XYZ direction in directions)
                {
                    // Cast ray from current position
                    IList<ReferenceWithContext> references = refIntersector.Find(location, direction);

                    foreach (ReferenceWithContext rwc in references)
                    {
                        double distance = rwc.Proximity;

                        // If within range and closer
                        if (distance <= radius && distance < minDistance)
                        {
                            minDistance = distance;
                            nearestHost = doc.GetElement(rwc.GetReference().ElementId);
                        }
                    }
                }

                return nearestHost;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", $"Error getting nearest host element: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Finds the nearest wall to a point using wall location-line distance calculation.
        /// More reliable than ray-casting for door/window placement.
        /// </summary>
        /// <param name="doc">Current Revit document</param>
        /// <param name="point">Target point (internal units, feet)</param>
        /// <param name="level">Level to filter walls on</param>
        /// <param name="tolerance">Extra tolerance beyond half wall width (feet). Default ~5mm.</param>
        /// <returns>Tuple of (wall, projectedPoint, wallDirection, distance) or null</returns>
        public static (Wall wall, XYZ projectedPoint, XYZ wallDirection, double distance)?
            GetNearestWallByLocationLine(
                this Document doc,
                XYZ point,
                Level level,
                double tolerance = 5.0 / 304.8)
        {
            if (doc == null || point == null || level == null)
                return null;

            // Collect all walls on the given level
            var walls = new FilteredElementCollector(doc)
                .OfClass(typeof(Wall))
                .Cast<Wall>()
                .Where(w =>
                {
                    Parameter baseLevelParam = w.get_Parameter(BuiltInParameter.WALL_BASE_CONSTRAINT);
                    return baseLevelParam != null && baseLevelParam.AsElementId() == level.Id;
                })
                .ToList();

            Wall bestWall = null;
            XYZ bestProjection = null;
            XYZ bestDirection = null;
            double bestDistance = double.MaxValue;

            foreach (Wall wall in walls)
            {
                LocationCurve locCurve = wall.Location as LocationCurve;
                if (locCurve == null) continue;

                Curve curve = locCurve.Curve;
                if (curve == null) continue;

                // Use Curve.Project() which handles both lines and arcs
                IntersectionResult ir = curve.Project(new XYZ(point.X, point.Y, curve.GetEndPoint(0).Z));
                if (ir == null) continue;

                XYZ projectedPt = ir.XYZPoint;
                double distance = new XYZ(point.X - projectedPt.X, point.Y - projectedPt.Y, 0).GetLength();

                // Check if point is within half the wall width + tolerance
                double halfWidth = wall.Width / 2.0;
                if (distance <= halfWidth + tolerance && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestWall = wall;
                    bestProjection = new XYZ(projectedPt.X, projectedPt.Y, point.Z);

                    // Compute wall direction from curve tangent at projected parameter
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    bestDirection = new XYZ(p1.X - p0.X, p1.Y - p0.Y, 0).Normalize();
                }
            }

            if (bestWall == null)
                return null;

            return (bestWall, bestProjection, bestDirection, bestDistance);
        }

        /// <summary>
        /// Highlights the given face.
        /// </summary>
        /// <param name="doc">当前文档</param>
        /// <param name="faceRef">要高亮显示的面Reference</param>
        /// <param name="duration">高亮持续时间(毫秒)，默认3000毫秒</param>
        public static void HighlightFace(this Document doc, Reference faceRef)
        {
            if (faceRef == null) return;

            // Get solid fill pattern
            FillPatternElement solidFill = new FilteredElementCollector(doc)
                .OfClass(typeof(FillPatternElement))
                .Cast<FillPatternElement>()
                .FirstOrDefault(x => x.GetFillPattern().IsSolidFill);

            if (solidFill == null)
            {
                TaskDialog.Show("Error", "Solid fill pattern not found.");
                return;
            }

            // Create override graphics
            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
            ogs.SetSurfaceForegroundPatternColor(new Color(255, 0, 0));
            ogs.SetSurfaceForegroundPatternId(solidFill.Id);
            ogs.SetSurfaceTransparency(0);

            // Highlight
            doc.ActiveView.SetElementOverrides(faceRef.ElementId, ogs);
        }

        /// <summary>
        /// Extracts the two principal direction vectors of the face.
        /// </summary>
        /// <param name="face">输入面</param>
        /// <returns>包含主方向和次方向的元组</returns>
        /// <exception cref="ArgumentNullException">当面为空时抛出</exception>
        /// <exception cref="ArgumentException">Thrown when the face has no valid edge loops.</exception>
        /// <exception cref="InvalidOperationException">当无法提取有效方向时抛出</exception>
        public static (XYZ PrimaryDirection, XYZ SecondaryDirection) GetMainDirections(this Face face)
        {
            // 1. 参数验证
            if (face == null)
                throw new ArgumentNullException(nameof(face), "面不能为空");

            // Face normal
            XYZ faceNormal = face.ComputeNormal(new UV(0.5, 0.5));

            // Outer contour
            EdgeArrayArray edgeLoops = face.EdgeLoops;
            if (edgeLoops.Size == 0)
                throw new ArgumentException("Face has no valid edge loops.", nameof(face));

            // First loop is usually outer contour
            EdgeArray outerLoop = edgeLoops.get_Item(0);

            List<XYZ> edgeDirections = new List<XYZ>();
            List<double> edgeLengths = new List<double>();

            foreach (Edge edge in outerLoop)
            {
                Curve curve = edge.AsCurve();
                XYZ startPoint = curve.GetEndPoint(0);
                XYZ endPoint = curve.GetEndPoint(1);

                // Vector from start to end
                XYZ direction = endPoint - startPoint;
                double length = direction.GetLength();

                // Skip very short edges
                if (length > 1e-10)
                {
                    edgeDirections.Add(direction.Normalize());
                    edgeLengths.Add(length);
                }
            }

            if (edgeDirections.Count < 4)
            {
                throw new ArgumentException("Face does not have enough edges to form a valid shape.", nameof(face));
            }

            List<List<int>> directionGroups = new List<List<int>>();

            for (int i = 0; i < edgeDirections.Count; i++)
            {
                bool foundGroup = false;
                XYZ currentDirection = edgeDirections[i];

                // 尝试将当前边加入已有的方向组
                for (int j = 0; j < directionGroups.Count; j++)
                {
                    var group = directionGroups[j];
                    XYZ groupAvgDir = CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths);

                    double dotProduct = Math.Abs(groupAvgDir.DotProduct(currentDirection));
                    if (dotProduct > 0.8)
                    {
                        group.Add(i);
                        foundGroup = true;
                        break;
                    }
                }

                if (!foundGroup)
                {
                    List<int> newGroup = new List<int> { i };
                    directionGroups.Add(newGroup);
                }
            }

            List<double> groupWeights = new List<double>();
            List<XYZ> groupDirections = new List<XYZ>();

            foreach (var group in directionGroups)
            {
                double totalLength = 0;
                foreach (int edgeIndex in group)
                {
                    totalLength += edgeLengths[edgeIndex];
                }
                groupWeights.Add(totalLength);

                groupDirections.Add(CalculateWeightedAverageDirection(group, edgeDirections, edgeLengths));
            }

            int[] sortedIndices = Enumerable.Range(0, groupDirections.Count)
                .OrderByDescending(i => groupWeights[i])
                .ToArray();

            if (groupDirections.Count >= 2)
            {
                int primaryIndex = sortedIndices[0];
                int secondaryIndex = sortedIndices[1];

                return (
                    PrimaryDirection: groupDirections[primaryIndex],
                    SecondaryDirection: groupDirections[secondaryIndex]
                );
            }
            else if (groupDirections.Count == 1)
            {
                XYZ primaryDirection = groupDirections[0];
                XYZ secondaryDirection = faceNormal.CrossProduct(primaryDirection).Normalize();

                return (
                    PrimaryDirection: primaryDirection,
                    SecondaryDirection: secondaryDirection
                );
            }
            else
            {
                throw new InvalidOperationException("Could not extract valid directions from face.");
            }
        }

        /// <summary>
        /// Calculates the weighted average direction of a set of edges by length.
        /// </summary>
        /// <param name="edgeIndices">Indices of edges.</param>
        /// <param name="directions">Direction vectors of all edges.</param>
        /// <param name="lengths">Lengths of all edges.</param>
        /// <returns>Normalized weighted average direction.</returns>
        public static XYZ CalculateWeightedAverageDirection(List<int> edgeIndices, List<XYZ> directions, List<double> lengths)
        {
            if (edgeIndices.Count == 0)
                return null;

            double sumX = 0, sumY = 0, sumZ = 0;
            XYZ referenceDir = directions[edgeIndices[0]];

            foreach (int i in edgeIndices)
            {
                XYZ currentDir = directions[i];

                // Dot product to decide if direction should be flipped
                double dot = referenceDir.DotProduct(currentDir);

                double factor = (dot >= 0) ? lengths[i] : -lengths[i];
                sumX += currentDir.X * factor;
                sumY += currentDir.Y * factor;
                sumZ += currentDir.Z * factor;
            }

            // Combine and normalize
            XYZ avgDir = new XYZ(sumX, sumY, sumZ);
            double magnitude = avgDir.GetLength();

            // Avoid zero vector
            if (magnitude < 1e-10)
                return referenceDir;

            return avgDir.Normalize();
        }

        /// <summary>
        /// Checks whether three vectors satisfy the right-hand rule and are mutually perpendicular.
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="indexFinger">食指方向向量</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">判断的容差，默认为1e-6</param>
        /// <returns>True if the three vectors satisfy the right-hand rule and are perpendicular.</returns>
        public static bool IsRightHandRuleCompliant(this XYZ thumb, XYZ indexFinger, XYZ middleFinger, double tolerance = 1e-6)
        {
            // Check mutual perpendicularity
            double dotThumbIndex = Math.Abs(thumb.DotProduct(indexFinger));
            double dotThumbMiddle = Math.Abs(thumb.DotProduct(middleFinger));
            double dotIndexMiddle = Math.Abs(indexFinger.DotProduct(middleFinger));

            bool areOrthogonal = (dotThumbIndex <= tolerance) &&
                                  (dotThumbMiddle <= tolerance) &&
                                  (dotIndexMiddle <= tolerance);

            // Check right-hand rule only when perpendicular
            if (!areOrthogonal)
                return false;

            // Cross product dot thumb = right-hand rule
            XYZ crossProduct = indexFinger.CrossProduct(middleFinger);
            double rightHandTest = crossProduct.DotProduct(thumb);

            // Positive dot = right-hand rule
            return rightHandTest > tolerance;
        }

        /// <summary>
        /// Generates the index-finger direction from thumb and middle (right-hand rule).
        /// </summary>
        /// <param name="thumb">拇指方向向量</param>
        /// <param name="middleFinger">中指方向向量</param>
        /// <param name="tolerance">垂直判断的容差，默认为1e-6</param>
        /// <returns>Index direction, or null if inputs are not perpendicular.</returns>
        public static XYZ GenerateIndexFinger(this XYZ thumb, XYZ middleFinger, double tolerance = 1e-6)
        {
            // Normalize inputs
            XYZ normalizedThumb = thumb.Normalize();
            XYZ normalizedMiddleFinger = middleFinger.Normalize();

            // Check perpendicular (dot near 0)
            double dotProduct = normalizedThumb.DotProduct(normalizedMiddleFinger);

            // Not perpendicular if dot exceeds tolerance
            if (Math.Abs(dotProduct) > tolerance)
            {
                return null;
            }

            // Cross product for index direction, then negate
            XYZ indexFinger = normalizedMiddleFinger.CrossProduct(normalizedThumb).Negate();

            // Return normalized index direction
            return indexFinger.Normalize();
        }

        /// <summary>
        /// Creates or gets a level at the given height.
        /// </summary>
        /// <param name="doc">revit文档</param>
        /// <param name="elevation">标高高度（ft）</param>
        /// <param name="levelName">标高名称</param>
        /// <returns></returns>
        public static Level CreateOrGetLevel(this Document doc, double elevation, string levelName)
        {
            // Find existing level at height
            Level existingLevel = new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .FirstOrDefault(l => Math.Abs(l.Elevation - elevation) < 0.1 / 304.8);

            if (existingLevel != null)
                return existingLevel;

            // Create new level
            Level newLevel = Level.Create(doc, elevation);
            // 设置标高名称
            Level namesakeLevel = new FilteredElementCollector(doc)
                 .OfClass(typeof(Level))
                 .Cast<Level>()
                 .FirstOrDefault(l => l.Name == levelName);
            if (namesakeLevel != null)
            {
                levelName = $"{levelName}_{newLevel.Id.GetValue()}";
            }
            newLevel.Name = levelName;

            return newLevel;
        }

        /// <summary>
        /// 查找距离给定高度最近的标高
        /// </summary>
        /// <param name="doc">当前Revit文档</param>
        /// <param name="height">目标高度（Revit内部单位）</param>
        /// <returns>距离目标高度最近的标高，若文档中没有标高则返回null</returns>
        public static Level FindNearestLevel(this Document doc, double height)
        {
            if (doc == null)
                throw new ArgumentNullException(nameof(doc), "文档不能为空");

            // 直接使用LINQ查询获取距离最近的标高
            return new FilteredElementCollector(doc)
                .OfClass(typeof(Level))
                .Cast<Level>()
                .OrderBy(level => Math.Abs(level.Elevation - height))
                .FirstOrDefault();
        }

        ///// <summary>
        ///// 刷新视图并添加延迟
        ///// </summary>
        //public static void Refresh(this Document doc, int waitingTime = 0, bool allowOperation = true)
        //{
        //    UIApplication uiApp = new UIApplication(doc.Application);
        //    UIDocument uiDoc = uiApp.ActiveUIDocument;

        //    // 检查文档是否可修改
        //    if (uiDoc.Document.IsModifiable)
        //    {
        //        // 更新模型
        //        uiDoc.Document.Regenerate();
        //    }
        //    // 更新界面
        //    uiDoc.RefreshActiveView();

        //    // 延迟等待
        //    if (waitingTime != 0)
        //    {
        //        System.Threading.Thread.Sleep(waitingTime);
        //    }

        //    // 允许用户进行非安全操作
        //    if (allowOperation)
        //    {
        //        System.Windows.Forms.Application.DoEvents();
        //    }
        //}

        /// <summary>
        /// 将指定的消息保存到桌面的指定文件中（默认覆盖文件）
        /// </summary>
        /// <param name="message">要保存的消息内容</param>
        /// <param name="fileName">目标文件名</param>
        public static void SaveToDesktop(this string message, string fileName = "temp.json", bool isAppend = false)
        {
            // 确保 logName 包含后缀
            if (!Path.HasExtension(fileName))
            {
                fileName += ".txt"; // 默认添加 .txt 后缀
            }

            // 获取桌面路径
            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);

            // 组合完整的文件路径
            string filePath = Path.Combine(desktopPath, fileName);

            // 写入文件（覆盖模式）
            using (StreamWriter sw = new StreamWriter(filePath, isAppend))
            {
                sw.WriteLine($"{message}");
            }
        }

    }
}
