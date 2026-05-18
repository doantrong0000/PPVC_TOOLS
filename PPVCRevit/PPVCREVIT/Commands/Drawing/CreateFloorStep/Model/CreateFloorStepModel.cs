using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreateFloorStep.Model
{
    public class FloorData
    {
        public Floor FloorElement { get; set; }
        public Transform LinkTransform { get; set; } // Transform của file link chứa sàn này
        public string SourceName { get; set; }        // Tên file nguồn (dùng để debug nếu cần)
    }

    public class CreateFloorStepModel
    {
        public static void CreateStepBetweenFloors(Document doc, UIDocument uidoc, List<FloorData> allFloorData)
        {
            if (allFloorData.Count < 2)
            {
                TaskDialog.Show("Thông báo", "Vui lòng chọn ít nhất 2 sàn cạnh nhau.");
                return;
            }

            // Thu thập Family Symbols tại File Chủ
            var stepSymbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(x => x.FamilyName.Equals("StepSymbol"))
                .ToList();

            FamilySymbol symbolRL = stepSymbols.FirstOrDefault(x => x.Name.Equals("RL"));
            FamilySymbol symbolLR = stepSymbols.FirstOrDefault(x => x.Name.Equals("LR"));

            if (symbolRL == null || symbolLR == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy đủ Type 'RL' và 'LR' trong Family 'StepSymbol' tại File Chủ.");
                return;
            }

            int countCreated = 0;

            using (Transaction tx = new Transaction(doc, "Batch Create Steps Across Host & Links"))
            {
                tx.Start();

                if (!symbolRL.IsActive) symbolRL.Activate();
                if (!symbolLR.IsActive) symbolLR.Activate();

                // Vòng lặp so sánh cặp chéo giữa các sàn
                for (int i = 0; i < allFloorData.Count; i++)
                {
                    for (int j = i + 1; j < allFloorData.Count; j++)
                    {
                        FloorData fData1 = allFloorData[i];
                        FloorData fData2 = allFloorData[j];

                        // Tính cao độ tuyệt đối của mặt Top dựa theo hệ tọa độ File Chủ
                        double elev1 = GetAbsoluteFloorTopElevation(fData1);
                        double elev2 = GetAbsoluteFloorTopElevation(fData2);

                        if (Math.Abs(elev1 - elev2) < 0.001) continue; // Cùng cao độ thì bỏ qua

                        // Tìm cạnh chung
                        Line sharedEdge = FindSharedEdgeCrossLinks(fData1, fData2);
                        if (sharedEdge == null) continue;

                        // ==========================================
                        // XỬ LÝ HÌNH HỌC & ĐẶT STEP
                        // ==========================================
                        XYZ p1 = sharedEdge.GetEndPoint(0);
                        XYZ p2 = sharedEdge.GetEndPoint(1);
                        XYZ dir = (p2 - p1).Normalize();

                        bool isVerticalTendency = Math.Abs(dir.Y) > Math.Abs(dir.X);
                        if (isVerticalTendency)
                        {
                            if (dir.Y > 0.0001) dir = -dir;
                        }
                        else
                        {
                            if (dir.X < -0.0001) dir = -dir;
                        }

                        XYZ midpoint = sharedEdge.Evaluate(0.5, true);

                        // Lấy tâm dựa trên Edges đã biến đổi để tránh sai sót BoundingBox giữa các file link
                        XYZ center1 = GetAbsoluteCenterFromEdges(fData1);
                        XYZ vecTo1 = center1 - midpoint;

                        double crossZ = dir.X * vecTo1.Y - dir.Y * vecTo1.X;
                        bool isFloor1Left = crossZ > 0;
                        bool isFloor1Higher = elev1 > elev2;

                        FamilySymbol targetSymbol = (isFloor1Left == isFloor1Higher) ? symbolRL : symbolLR;

                        View activeView = uidoc.ActiveView;
                        double targetZ = Math.Max(elev1, elev2);
                        XYZ placementPoint = new XYZ(midpoint.X, midpoint.Y, targetZ);

                        // Bắt dính (Snap) vào cạnh mặt trên của sàn cao hơn
                        FloorData higherFloorData = isFloor1Higher ? fData1 : fData2;
                        List<Line> higherTopEdges = GetAbsoluteTopEdges(higherFloorData);

                        double minDistance = double.MaxValue;
                        XYZ snappedPoint = placementPoint;

                        foreach (Line edge in higherTopEdges)
                        {
                            IntersectionResult result = edge.Project(placementPoint);
                            if (result != null)
                            {
                                if (result.Distance < minDistance)
                                {
                                    minDistance = result.Distance;
                                    snappedPoint = result.XYZPoint;
                                }
                            }
                        }

                        placementPoint = snappedPoint;

                        // Đặt cấu kiện Step trực tiếp vào File Chủ
                        FamilyInstance instance = doc.Create.NewFamilyInstance(placementPoint, targetSymbol, activeView);

                        // Xoay cấu kiện đúng hướng cạnh chung
                        double angle = Math.Atan2(dir.Y, dir.X) + Math.PI / 2;
                        if (Math.Abs(angle) > 0.001)
                        {
                            Line axis = Line.CreateBound(placementPoint, placementPoint + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
                        }

                        // Gán thông số độ cao giật cấp (quy đổi ra mm)
                        double stepHeightValue = Math.Round(Math.Abs(elev1 - elev2) * 304.8, 1);
                        Parameter stepHeightParam = instance.LookupParameter("StepHeight");
                        if (stepHeightParam != null && !stepHeightParam.IsReadOnly)
                        {
                            stepHeightParam.Set(stepHeightValue.ToString());
                        }

                        countCreated++;
                    }
                }

                tx.Commit();
            }

            if (countCreated > 0)
                TaskDialog.Show("Thành công", $"Đã tạo thành công {countCreated} vị trí giật cấp.");
            else
                TaskDialog.Show("Thông báo", "Không tìm thấy giao điểm giật cấp nào giữa các sàn đã chọn.");
        }

        private static double GetAbsoluteFloorTopElevation(FloorData fData)
        {
            Floor floor = fData.FloorElement;
            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = p != null ? p.AsDouble() : 0;

            Level level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElev = level != null ? level.Elevation : 0;

            double localZ = levelElev + offset;

            XYZ localPoint = new XYZ(0, 0, localZ);
            XYZ absolutePoint = fData.LinkTransform.OfPoint(localPoint);

            return absolutePoint.Z;
        }

        private static Line FindSharedEdgeCrossLinks(FloorData f1, FloorData f2)
        {
            List<Line> edges1 = GetAbsoluteAllEdges(f1);
            List<Line> edges2 = GetAbsoluteAllEdges(f2);

            List<Line> listLine = new List<Line>();
            foreach (Line l1 in edges1)
            {
                foreach (Line l2 in edges2)
                {
                    Line overlap = GetOverlap2D(l1, l2);
                    if (overlap != null)
                    {
                        listLine.Add(overlap);
                    }
                }
            }

            if (listLine.Count == 0) return null;
            return listLine.OrderByDescending(l => l.Length).FirstOrDefault();
        }

        private static Line GetOverlap2D(Line l1, Line l2)
        {
            XYZ p1 = new XYZ(l1.GetEndPoint(0).X, l1.GetEndPoint(0).Y, 0);
            XYZ p2 = new XYZ(l1.GetEndPoint(1).X, l1.GetEndPoint(1).Y, 0);
            XYZ q1 = new XYZ(l2.GetEndPoint(0).X, l2.GetEndPoint(0).Y, 0);
            XYZ q2 = new XYZ(l2.GetEndPoint(1).X, l2.GetEndPoint(1).Y, 0);

            XYZ v1 = (p2 - p1).Normalize();
            XYZ v2 = (q2 - q1).Normalize();
            if (v1.IsAlmostEqualTo(XYZ.Zero, 0.00001) || v2.IsAlmostEqualTo(XYZ.Zero, 0.00001))
                return null;

            if (!IsParallelWithTolerance(v1, v2, 0.02)) return null;

            Line infiniteL1 = Line.CreateUnbound(p1, v1);
            if (infiniteL1.Distance(q1) > 0.006) return null;

            double tq1 = GetProjParam(p1, p2, q1);
            double tq2 = GetProjParam(p1, p2, q2);

            double tStart = Math.Max(0, Math.Min(tq1, tq2));
            double tEnd = Math.Min(1, Math.Max(tq1, tq2));

            if (tEnd - tStart > 0.01)
            {
                return Line.CreateBound(p1 + (p2 - p1) * tStart, p1 + (p2 - p1) * tEnd);
            }
            return null;
        }

        private static bool IsParallelWithTolerance(XYZ vector1, XYZ vector2, double tolerance = 0.01)
        {
            XYZ crossProg = vector1.CrossProduct(vector2);
            return crossProg.GetLength() <= tolerance;
        }

        private static double GetProjParam(XYZ a, XYZ b, XYZ p)
        {
            XYZ ap = p - a;
            XYZ ab = b - a;
            return ap.DotProduct(ab) / ab.DotProduct(ab);
        }

        private static List<Line> GetAbsoluteTopEdges(FloorData fData)
        {
            List<Line> results = new List<Line>();
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = fData.FloorElement.get_Geometry(opt);
            Transform tf = fData.LinkTransform;

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        XYZ transformedNormal = tf.OfVector(face.ComputeNormal(UV.Zero)).Normalize();

                        if (transformedNormal.IsAlmostEqualTo(XYZ.BasisZ, 0.01))
                        {
                            foreach (EdgeArray loop in face.EdgeLoops)
                            {
                                foreach (Edge edge in loop)
                                {
                                    if (edge.AsCurve() is Line line)
                                        results.Add(line.CreateTransformed(tf) as Line);
                                }
                            }
                        }
                    }
                }
            }
            return results;
        }

        private static List<Line> GetAbsoluteAllEdges(FloorData fData)
        {
            List<Line> results = new List<Line>();
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = fData.FloorElement.get_Geometry(opt);
            Transform tf = fData.LinkTransform;

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        foreach (EdgeArray loop in face.EdgeLoops)
                        {
                            foreach (Edge edge in loop)
                            {
                                if (edge.AsCurve() is Line line)
                                    results.Add(line.CreateTransformed(tf) as Line);
                            }
                        }
                    }
                }
            }
            return results;
        }

        private static XYZ GetAbsoluteCenterFromEdges(FloorData fData)
        {
            var edges = GetAbsoluteTopEdges(fData);
            if (edges.Count == 0) return XYZ.Zero;
            double sumX = 0, sumY = 0, sumZ = 0;
            int ptCount = 0;
            foreach (var edge in edges)
            {
                sumX += edge.GetEndPoint(0).X + edge.GetEndPoint(1).X;
                sumY += edge.GetEndPoint(0).Y + edge.GetEndPoint(1).Y;
                sumZ += edge.GetEndPoint(0).Z + edge.GetEndPoint(1).Z;
                ptCount += 2;
            }
            return new XYZ(sumX / ptCount, sumY / ptCount, sumZ / ptCount);
        }
    }
}
