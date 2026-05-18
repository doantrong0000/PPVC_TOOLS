using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreateFloorStep.Model
{
    public class FloorData2
    {
        public Floor FloorElement { get; set; }
        public Transform LinkTransform { get; set; } // Transform của file link chứa sàn này
        public string SourceName { get; set; }        // Tên file nguồn (dùng để debug nếu cần)
    }

    public class CreateFloorStepModel2
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

                // Vòng lặp so sánh cặp chéo giữa các sàn dựa trên hình học thực tế (Đã Join/Cut)
                for (int i = 0; i < allFloorData.Count; i++)
                {
                    for (int j = i + 1; j < allFloorData.Count; j++)
                    {
                        FloorData fData1 = allFloorData[i];
                        FloorData fData2 = allFloorData[j];

                        // 1. Lấy danh sách các cạnh thực tế thuộc mặt trên (Top Face) của từng sàn
                        List<Line> topEdges1 = GetAbsoluteTopEdges(fData1);
                        List<Line> topEdges2 = GetAbsoluteTopEdges(fData2);

                        if (topEdges1.Count == 0 || topEdges2.Count == 0) continue;

                        // 2. Tìm cạnh chung thực tế dựa trên hình chiếu 2D phẳng
                        Line realSharedEdge = FindRealSharedEdge(topEdges1, topEdges2);
                        if (realSharedEdge == null) continue; // Không tiếp xúc thực tế thì bỏ qua

                        // 3. Xác định cao độ tuyệt đối Z trực tiếp từ đường biên Top Face thực tế
                        double z1 = topEdges1.First().GetEndPoint(0).Z;
                        double z2 = topEdges2.First().GetEndPoint(0).Z;

                        if (Math.Abs(z1 - z2) < 0.001) continue; // Cùng cao độ thực tế thì bỏ qua

                        bool isFloor1Higher = z1 > z2;

                        // =========================================================================
                        // XỬ LÝ HÌNH HỌC VÀ ĐẶT STEP TRỰC TIẾP LÊN CẠNH THỰC TẾ
                        // =========================================================================
                        // Lấy trung điểm 2D/3D của cạnh chung thực tế đã bị cắt gọt
                        XYZ midpoint = realSharedEdge.Evaluate(0.5, true);

                        // Cao độ đặt mặc định bằng đúng mặt sàn cao hơn
                        double targetZ = isFloor1Higher ? z1 : z2;
                        XYZ placementPoint = new XYZ(midpoint.X, midpoint.Y, targetZ);

                        // Tính toán hướng vector cạnh chung để xoay và xét Left/Right
                        XYZ p1 = realSharedEdge.GetEndPoint(0);
                        XYZ p2 = realSharedEdge.GetEndPoint(1);
                        XYZ dir = (p2 - p1).Normalize();

                        if (Math.Abs(dir.Y) > Math.Abs(dir.X))
                        {
                            if (dir.Y > 0.0001) dir = -dir;
                        }
                        else
                        {
                            if (dir.X < -0.0001) dir = -dir;
                        }

                        // Tính tâm của sàn 1 dựa trên các cạnh thực tế để xét Left/Right chính xác
                        XYZ center1 = GetAbsoluteCenterFromEdges(topEdges1);
                        XYZ vecTo1 = center1 - placementPoint;

                        double crossZ = dir.X * vecTo1.Y - dir.Y * vecTo1.X;
                        bool isFloor1Left = crossZ > 0;

                        FamilySymbol targetSymbol = (isFloor1Left == isFloor1Higher) ? symbolRL : symbolLR;

                        // Đặt Family
                        View activeView = uidoc.ActiveView;
                        FamilyInstance instance = doc.Create.NewFamilyInstance(placementPoint, targetSymbol, activeView);

                        // Xoay cấu kiện đúng hướng cạnh chung thực tế
                        double angle = Math.Atan2(dir.Y, dir.X) + Math.PI / 2;
                        if (Math.Abs(angle) > 0.001)
                        {
                            Line axis = Line.CreateBound(placementPoint, placementPoint + XYZ.BasisZ);
                            ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
                        }

                        // Gán thông số độ cao giật cấp thực tế (quy đổi ra mm)
                        double stepHeightValue = Math.Round(Math.Abs(z1 - z2) * 304.8, 1);
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
                TaskDialog.Show("Thành công", $"Đã tạo thành công {countCreated} vị trí giật cấp thực tế.");
            else
                TaskDialog.Show("Thông báo", "Không tìm thấy giao điểm giật cấp thực tế nào giữa các sàn đã chọn.");
        }

        // TÌM CẠNH CHUNG THỰC TẾ: Đối chiếu các cạnh Top thực tế của 2 sàn
        private static Line FindRealSharedEdge(List<Line> edges1, List<Line> edges2)
        {
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

        // HÀM ĐÃ THAY ĐỔI: Thêm ComputeReferences để lấy chính xác các Face thực tế sau Join/Cut
        private static List<Line> GetAbsoluteTopEdges(FloorData fData)
        {
            List<Line> results = new List<Line>();
            Options opt = new Options
            {
                DetailLevel = ViewDetailLevel.Fine,
                ComputeReferences = true // Bắt buộc tính toán hình học sau khi bị Join / Cut hình khối
            };

            GeometryElement geo = fData.FloorElement.get_Geometry(opt);
            Transform tf = fData.LinkTransform;

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0)
                {
                    foreach (Face face in solid.Faces)
                    {
                        XYZ localNormal = face.ComputeNormal(UV.Zero);
                        XYZ transformedNormal = tf.OfVector(localNormal).Normalize();

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

        // HÀM ĐÃ THAY ĐỔI: Chuyển sang nhận tham số đầu vào là danh sách Edges thực tế đã xử lý
        private static XYZ GetAbsoluteCenterFromEdges(List<Line> edges)
        {
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