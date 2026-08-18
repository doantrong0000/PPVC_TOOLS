using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Helpers
{
    public static class GeometryMatchHelper
    {
        public static Element FindMatchingElement(Document doc, Element sourceElem, View targetView)
        {
            if (sourceElem == null) return null;

            // If it's a Grid or Level, they are unique project-wide
            if (sourceElem is Grid || sourceElem is Level || sourceElem.Category?.Id.Value == (int)BuiltInCategory.OST_Grids || sourceElem.Category?.Id.Value == (int)BuiltInCategory.OST_Levels)
            {
                return sourceElem;
            }

            // For other elements, try to find an element of the same category and type in the target view
            var collector = new FilteredElementCollector(doc, targetView.Id)
                .OfCategoryId(sourceElem.Category.Id)
                .WhereElementIsNotElementType();

            foreach (var targetElem in collector)
            {
                if (targetElem.GetTypeId() == sourceElem.GetTypeId())
                {
                    // For simplicity, we just return the first matching element by Type.
                    // In a more advanced version, we would check relative bounding box positions.
                    return targetElem;
                }
            }

            return null;
        }

        public static Reference FindMatchingReference(Document doc, Element targetElem, View targetView, XYZ dimDirection, XYZ sourcePoint)
        {
            // Note: We use dimDirection to find faces that are perpendicular to the dimension line
            // meaning the face normal is parallel to dimDirection.
            // We use sourcePoint to pick the face closest to the original dimension reference point.

            Options opt = new Options();
            opt.ComputeReferences = true;
            opt.IncludeNonVisibleObjects = true;
            opt.View = targetView;

            GeometryElement targetGeom = targetElem.get_Geometry(opt);
            if (targetGeom == null) return null;

            return FindMatchingFaceByDirection(targetGeom, dimDirection, sourcePoint);
        }

        private static Reference FindMatchingFaceByDirection(GeometryElement targetGeom, XYZ dimDirection, XYZ expectedPoint)
        {
            Reference bestRef = null;
            double minDistance = double.MaxValue;

            foreach (GeometryObject obj in targetGeom)
            {
                if (obj is Solid solid && solid.Faces.Size > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace planarFace)
                        {
                            // Check if face normal is parallel to dimension direction (meaning face is perpendicular to dim line)
                            if (planarFace.FaceNormal.IsAlmostEqualTo(dimDirection) || planarFace.FaceNormal.IsAlmostEqualTo(dimDirection.Negate()))
                            {
                                // Among parallel faces, find the one closest to the expected point
                                BoundingBoxUV bbox = planarFace.GetBoundingBox();
                                UV center = (bbox.Min + bbox.Max) / 2.0;
                                XYZ faceCenter = planarFace.Evaluate(center);
                                
                                // Calculate distance along the dimension direction
                                double distance = faceCenter.DistanceTo(expectedPoint);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    bestRef = planarFace.Reference;
                                }
                            }
                        }
                    }
                }
                else if (obj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    Reference refFound = FindMatchingFaceByDirection(instGeom, dimDirection, expectedPoint);
                    if (refFound != null)
                    {
                        // We shouldn't just return the first one from an instance if we want the closest,
                        // but for simplicity we'll assume instances don't have multiple overlapping identical faces.
                        // Ideally, we'd compare distances here too.
                        return refFound;
                    }
                }
            }

            // Fallback: If no planar face found, try to find an edge perpendicular to dim direction
            if (bestRef == null)
            {
                bestRef = FindMatchingEdgeByDirection(targetGeom, dimDirection, expectedPoint);
            }

            return bestRef;
        }

        private static Reference FindMatchingEdgeByDirection(GeometryElement targetGeom, XYZ dimDirection, XYZ expectedPoint)
        {
            Reference bestRef = null;
            double minDistance = double.MaxValue;

            foreach (GeometryObject obj in targetGeom)
            {
                if (obj is Solid solid && solid.Edges.Size > 0)
                {
                    foreach (Edge edge in solid.Edges)
                    {
                        Curve curve = edge.AsCurve();
                        if (curve is Line line)
                        {
                            // An edge is perpendicular to the dim direction if their dot product is 0
                            if (Math.Abs(line.Direction.DotProduct(dimDirection)) < 1e-6)
                            {
                                XYZ edgeCenter = line.Evaluate(0.5, true);
                                double distance = edgeCenter.DistanceTo(expectedPoint);
                                if (distance < minDistance)
                                {
                                    minDistance = distance;
                                    bestRef = edge.Reference;
                                }
                            }
                        }
                    }
                }
                else if (obj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    Reference refFound = FindMatchingEdgeByDirection(instGeom, dimDirection, expectedPoint);
                    if (refFound != null) return refFound;
                }
            }
            return bestRef;
        }
    }
}
