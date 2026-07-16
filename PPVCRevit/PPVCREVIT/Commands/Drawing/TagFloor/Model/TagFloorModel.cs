using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.TagFloor.Model
{
    public class TagFloorModel
    {
        public static void ProcessFloor(Document doc, Floor floor, Transform transform, FamilySymbol tagSymbol, View activeView, ref int tagCount)
        {
            List<Solid> solids = GetSolids(floor, transform);
            if (solids.Count == 0) return;

            List<Face> topFaces = new List<Face>();
            List<Face> bottomFaces = new List<Face>();
            GetFaces(solids, topFaces, bottomFaces);

            if (topFaces.Count == 0) return;

            double defaultThickness = GetDefaultFloorThickness(floor);

            foreach (Face topFace in topFaces)
            {
                XYZ normal = topFace.ComputeNormal(new UV(0.5, 0.5)).Normalize();
                bool isFaceSloped = Math.Abs(normal.Z - 1.0) > 0.001;

                if (isFaceSloped)
                {
                    // --- CASE 2: Sloped Face ---
                    List<XYZ> vertices = new List<XYZ>();
                    Mesh mesh = topFace.Triangulate();
                    if (mesh != null)
                    {
                        foreach (XYZ v in mesh.Vertices)
                        {
                            if (!vertices.Any(existing => existing.IsAlmostEqualTo(v, 0.001)))
                            {
                                vertices.Add(v);
                            }
                        }
                    }
                    else
                    {
                        foreach (EdgeArray loop in topFace.EdgeLoops)
                        {
                            foreach (Edge edge in loop)
                            {
                                Curve curve = edge.AsCurve();
                                if (curve != null)
                                {
                                    XYZ p0 = curve.GetEndPoint(0);
                                    XYZ p1 = curve.GetEndPoint(1);
                                    if (!vertices.Any(existing => existing.IsAlmostEqualTo(p0, 0.001))) vertices.Add(p0);
                                    if (!vertices.Any(existing => existing.IsAlmostEqualTo(p1, 0.001))) vertices.Add(p1);
                                }
                            }
                        }
                    }

                    List<int> thicknesses = new List<int>();
                    foreach (XYZ vertex in vertices)
                    {
                        double t = GetThicknessAtPoint(bottomFaces, vertex, defaultThickness);
                        int tMm = (int)Math.Round(t * 304.8);
                        if (!thicknesses.Contains(tMm))
                        {
                            thicknesses.Add(tMm);
                        }
                    }

                    string thicknessVal = "";
                    if (thicknesses.Count > 0)
                    {
                        int maxT = thicknesses.Max();
                        int minT = thicknesses.Min();
                        if (maxT != minT)
                        {
                            thicknessVal = $"{maxT}~{minT}";
                        }
                        else
                        {
                            thicknessVal = maxT.ToString();
                        }
                    }
                    else
                    {
                        thicknessVal = ((int)Math.Round(defaultThickness * 304.8)).ToString();
                    }

                    XYZ center = GetFaceCenter(topFace);
                    FamilyInstance tagInstance = doc.Create.NewFamilyInstance(center, tagSymbol, activeView);
                    SetThicknessParameter(tagInstance, thicknessVal);
                    tagCount++;
                }
                else
                {
                    // --- CASE 1: Flat Face ---
                    XYZ center = GetFaceCenter(topFace);
                    double t = GetThicknessAtPoint(bottomFaces, center, defaultThickness);
                    int tMm = (int)Math.Round(t * 304.8);
                    string thicknessVal = tMm.ToString();

                    FamilyInstance tagInstance = doc.Create.NewFamilyInstance(center, tagSymbol, activeView);
                    SetThicknessParameter(tagInstance, thicknessVal);
                    tagCount++;
                }
            }
        }

        private static List<Solid> GetSolids(Floor floor, Transform tf)
        {
            List<Solid> solids = new List<Solid>();
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine, ComputeReferences = true };
            GeometryElement geo = floor.get_Geometry(opt);
            if (geo != null)
            {
                ParseGeometry(geo, solids, tf);
            }
            return solids;
        }

        private static void ParseGeometry(GeometryElement geo, List<Solid> solids, Transform tf)
        {
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0.0001)
                {
                    Solid transformedSolid = SolidUtils.CreateTransformed(solid, tf);
                    solids.Add(transformedSolid);
                }
                else if (obj is GeometryInstance instance)
                {
                    Transform combinedTf = tf.Multiply(instance.Transform);
                    GeometryElement instanceGeo = instance.GetInstanceGeometry();
                    if (instanceGeo != null)
                    {
                        ParseGeometry(instanceGeo, solids, combinedTf);
                    }
                }
            }
        }

        private static void GetFaces(List<Solid> solids, List<Face> topFaces, List<Face> bottomFaces)
        {
            foreach (Solid solid in solids)
            {
                foreach (Face face in solid.Faces)
                {
                    XYZ normal = face.ComputeNormal(UV.Zero).Normalize();
                    if (normal.Z > 0.5)
                    {
                        topFaces.Add(face);
                    }
                    else if (normal.Z < -0.5)
                    {
                        bottomFaces.Add(face);
                    }
                }
            }
        }

        private static double GetThicknessAtPoint(List<Face> bottomFaces, XYZ point, double defaultThickness)
        {
            foreach (Face bottomFace in bottomFaces)
            {
                IntersectionResult proj = bottomFace.Project(point);
                if (proj != null)
                {
                    XYZ normal = bottomFace.ComputeNormal(UV.Zero).Normalize();
                    if (Math.Abs(normal.Z) > 0.001)
                    {
                        XYZ p0 = bottomFace.Evaluate(UV.Zero);
                        double zBottom = p0.Z - ((point.X - p0.X) * normal.X + (point.Y - p0.Y) * normal.Y) / normal.Z;
                        
                        IntersectionResult checkProj = bottomFace.Project(new XYZ(point.X, point.Y, zBottom));
                        if (checkProj != null)
                        {
                            double dx = checkProj.XYZPoint.X - point.X;
                            double dy = checkProj.XYZPoint.Y - point.Y;
                            double dist2D = Math.Sqrt(dx * dx + dy * dy);
                            if (dist2D < 0.05)
                            {
                                return Math.Abs(point.Z - zBottom);
                            }
                        }
                    }
                }
            }

            double min2DDist = double.MaxValue;
            double bestThickness = defaultThickness;
            foreach (Face bottomFace in bottomFaces)
            {
                IntersectionResult proj = bottomFace.Project(point);
                if (proj != null)
                {
                    double dx = proj.XYZPoint.X - point.X;
                    double dy = proj.XYZPoint.Y - point.Y;
                    double dist2D = Math.Sqrt(dx * dx + dy * dy);
                    if (dist2D < min2DDist)
                    {
                        min2DDist = dist2D;
                        XYZ normal = bottomFace.ComputeNormal(UV.Zero).Normalize();
                        if (Math.Abs(normal.Z) > 0.001)
                        {
                            XYZ p0 = bottomFace.Evaluate(UV.Zero);
                            double zBottom = p0.Z - ((point.X - p0.X) * normal.X + (point.Y - p0.Y) * normal.Y) / normal.Z;
                            bestThickness = Math.Abs(point.Z - zBottom);
                        }
                    }
                }
            }

            if (min2DDist < 1.0)
            {
                return bestThickness;
            }

            return defaultThickness;
        }

        private static double GetDefaultFloorThickness(Floor floor)
        {
            Parameter pDefault = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_DEFAULT_THICKNESS_PARAM);
            if (pDefault != null && pDefault.HasValue)
            {
                return pDefault.AsDouble();
            }
            FloorType floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
            if (floorType != null)
            {
                CompoundStructure cs = floorType.GetCompoundStructure();
                if (cs != null)
                {
                    return cs.GetWidth();
                }
            }
            return 0.0;
        }

        private static XYZ GetFaceCenter(Face face)
        {
            List<XYZ> vertices = new List<XYZ>();
            Mesh mesh = face.Triangulate();
            if (mesh != null)
            {
                foreach (XYZ v in mesh.Vertices)
                {
                    if (!vertices.Any(existing => existing.IsAlmostEqualTo(v, 0.001)))
                    {
                        vertices.Add(v);
                    }
                }
            }
            else
            {
                foreach (EdgeArray loop in face.EdgeLoops)
                {
                    foreach (Edge edge in loop)
                    {
                        Curve curve = edge.AsCurve();
                        if (curve != null)
                        {
                            XYZ p0 = curve.GetEndPoint(0);
                            XYZ p1 = curve.GetEndPoint(1);
                            if (!vertices.Any(existing => existing.IsAlmostEqualTo(p0, 0.001))) vertices.Add(p0);
                            if (!vertices.Any(existing => existing.IsAlmostEqualTo(p1, 0.001))) vertices.Add(p1);
                        }
                    }
                }
            }

            if (vertices.Count > 0)
            {
                double minX = vertices.Min(v => v.X);
                double maxX = vertices.Max(v => v.X);
                double minY = vertices.Min(v => v.Y);
                double maxY = vertices.Max(v => v.Y);
                double minZ = vertices.Min(v => v.Z);
                double maxZ = vertices.Max(v => v.Z);

                return new XYZ((minX + maxX) / 2.0, (minY + maxY) / 2.0, (minZ + maxZ) / 2.0);
            }

            BoundingBoxUV bbox = face.GetBoundingBox();
            UV centerUV = new UV((bbox.Min.U + bbox.Max.U) / 2.0, (bbox.Min.V + bbox.Max.V) / 2.0);
            return face.Evaluate(centerUV);
        }

        private static void SetThicknessParameter(FamilyInstance instance, string val)
        {
            Parameter p = instance.LookupParameter("thickness");
            if (p == null) p = instance.LookupParameter("Thickness");
            if (p == null) p = instance.LookupParameter("ThicknessSlab");
            if (p == null)
            {
                foreach (Parameter param in instance.Parameters)
                {
                    if (param.Definition.Name.Equals("thickness", StringComparison.OrdinalIgnoreCase) ||
                        param.Definition.Name.Equals("Thickness", StringComparison.OrdinalIgnoreCase) ||
                        param.Definition.Name.Equals("ThicknessSlab", StringComparison.OrdinalIgnoreCase))
                    {
                        p = param;
                        break;
                    }
                }
            }

            if (p != null && !p.IsReadOnly)
            {
                if (p.StorageType == StorageType.String)
                {
                    p.Set(val);
                }
                else if (p.StorageType == StorageType.Double)
                {
                    if (double.TryParse(val, out double valMm))
                    {
                        p.Set(valMm / 304.8);
                    }
                }
                else if (p.StorageType == StorageType.Integer)
                {
                    if (int.TryParse(val, out int valInt))
                    {
                        p.Set(valInt);
                    }
                }
            }
        }
    }
}
