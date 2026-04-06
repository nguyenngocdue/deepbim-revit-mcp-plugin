using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;

namespace DeepBimMCPTools
{
    public sealed class GeometrySurfaceExtractionService
    {
        private const double SquareFeetToSquareMeters = 0.09290304;

        public SurfaceExtractionResult ExtractFromSelection(UIApplication uiApp, SurfaceExtractionOptions options)
        {
            if (uiApp == null) throw new ArgumentNullException(nameof(uiApp));

            UIDocument uiDoc = uiApp.ActiveUIDocument ?? throw new InvalidOperationException("No active document.");
            Document doc = uiDoc.Document;

            Reference pickedRef;
            try
            {
                pickedRef = uiDoc.Selection.PickObject(ObjectType.Element, "Select an element to extract surfaces");
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                throw new InvalidOperationException("Selection canceled.");
            }

            Element element = doc.GetElement(pickedRef) ?? throw new InvalidOperationException("Selected element is invalid.");

            var geometryOptions = new Options
            {
                ComputeReferences = true,
                IncludeNonVisibleObjects = options.IncludeNonVisibleObjects,
                DetailLevel = ViewDetailLevel.Fine
            };

            GeometryElement geometry = element.get_Geometry(geometryOptions)
                ?? throw new InvalidOperationException("Selected element has no geometry.");

            var result = new SurfaceExtractionResult
            {
                ElementId = element.Id.IntegerValue,
                ElementUniqueId = element.UniqueId ?? string.Empty,
                ElementName = element.Name ?? string.Empty,
                CategoryName = element.Category?.Name ?? string.Empty
            };

            var faces = new List<Face>();
            CollectFaces(geometry, faces);

            int index = 0;
            foreach (var face in faces)
            {
                index++;
                var model = BuildSurfaceModel(doc, face, index, options);
                result.Surfaces.Add(model);
                result.TotalAreaSquareFeet += model.AreaSquareFeet;
            }

            result.SurfaceCount = result.Surfaces.Count;
            result.TotalAreaSquareMeters = result.TotalAreaSquareFeet * SquareFeetToSquareMeters;
            return result;
        }

        private static void CollectFaces(GeometryElement geometry, List<Face> faces)
        {
            foreach (GeometryObject obj in geometry)
            {
                if (obj is Solid solid)
                {
                    if (solid.Faces.Size == 0) continue;
                    foreach (Face face in solid.Faces)
                    {
                        faces.Add(face);
                    }
                    continue;
                }

                if (obj is GeometryInstance instance)
                {
                    var instanceGeometry = instance.GetInstanceGeometry();
                    if (instanceGeometry != null)
                    {
                        CollectFaces(instanceGeometry, faces);
                    }
                }
            }
        }

        private static SurfaceModel BuildSurfaceModel(Document doc, Face face, int index, SurfaceExtractionOptions options)
        {
            var model = new SurfaceModel
            {
                Index = index,
                SurfaceType = face.GetType().Name,
                IsPlanar = face is PlanarFace,
                AreaSquareFeet = face.Area,
                AreaSquareMeters = face.Area * SquareFeetToSquareMeters,
                EdgeLoopCount = face.EdgeLoops?.Size ?? 0,
                MaterialElementId = face.MaterialElementId?.IntegerValue ?? -1
            };

            if (face.Reference != null)
            {
                try
                {
                    model.StableReference = face.Reference.ConvertToStableRepresentation(doc);
                }
                catch
                {
                    model.StableReference = string.Empty;
                }
            }

            if (face is PlanarFace planar)
            {
                model.Normal = ToPoint(planar.FaceNormal);
            }

            var mesh = face.Triangulate();
            model.TriangleCount = mesh?.NumTriangles ?? 0;

            if (options.IncludeMeshVertices && mesh != null && mesh.Vertices != null)
            {
                int count = Math.Min(options.MaxVerticesPerSurface, mesh.Vertices.Count);
                for (int i = 0; i < count; i++)
                {
                    model.SampleVertices.Add(ToPoint(mesh.Vertices[i]));
                }
            }

            return model;
        }

        private static SurfacePointModel ToPoint(XYZ xyz)
        {
            return new SurfacePointModel
            {
                X = xyz?.X ?? 0,
                Y = xyz?.Y ?? 0,
                Z = xyz?.Z ?? 0
            };
        }
    }
}
