using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFloorStepCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Quét chọn nhiều sàn
                IList<Reference> selectedRefs = uidoc.Selection.PickObjects(ObjectType.Element, new FloorSelectionFilter(), "Quét chọn các sàn để tạo Step");

                List<Floor> floors = selectedRefs
                    .Select(r => doc.GetElement(r) as Floor)
                    .Where(f => f != null)
                    .ToList();

                if (floors.Count < 2)
                {
                    TaskDialog.Show("Thông báo", "Vui lòng chọn ít nhất 2 sàn cạnh nhau.");
                    return Result.Cancelled;
                }

                // 2. Lấy sẵn các Type Family để dùng trong vòng lặp (tăng hiệu suất)
                var stepSymbols = new FilteredElementCollector(doc)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .Where(x => x.FamilyName.Equals("StepSymbol"))
                    .ToList();

                FamilySymbol symbolRL = stepSymbols.FirstOrDefault(x => x.Name.Equals("RL"));
                FamilySymbol symbolLR = stepSymbols.FirstOrDefault(x => x.Name.Equals("LR"));

                if (symbolRL == null || symbolLR == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy đủ Type 'RL' và 'LR' trong Family 'StepSymbol'.");
                    return Result.Cancelled;
                }

                int countCreated = 0;

                using (Transaction tx = new Transaction(doc, "Create Floor Steps Batch"))
                {
                    tx.Start();

                    if (!symbolRL.IsActive) symbolRL.Activate();
                    if (!symbolLR.IsActive) symbolLR.Activate();

                    // 3. Tách ra các cặp sát nhau bằng vòng lặp lồng
                    for (int i = 0; i < floors.Count; i++)
                    {
                        for (int j = i + 1; j < floors.Count; j++)
                        {
                            Floor floor1 = floors[i];
                            Floor floor2 = floors[j];

                            // Kiểm tra cao độ
                            double elev1 = GetFloorTopElevation(floor1);
                            double elev2 = GetFloorTopElevation(floor2);

                            if (Math.Abs(elev1 - elev2) < 0.001) continue; // Cùng cao độ thì bỏ qua

                            // Tìm cạnh chung
                            Line sharedEdge = FindSharedEdgeBy2DProjection(floor1, floor2);
                            if (sharedEdge == null) continue; // Không sát nhau thì bỏ qua

                            // ==========================================
                            // 4. XỬ LÝ ĐẶT STEP CHO CẶP NÀY
                            // ==========================================
                            XYZ p1 = sharedEdge.GetEndPoint(0);
                            XYZ p2 = sharedEdge.GetEndPoint(1);
                            XYZ dir = (p2 - p1).Normalize();


                            bool isVerticalTendency = Math.Abs(dir.Y) > Math.Abs(dir.X);

                            if (isVerticalTendency)
                            {
                                // === XỬ LÝ THEO PHƯƠNG Y ===
                                // Nếu đang hướng xuống (Y âm) -> Đảo ngược để luôn hướng lên (Y dương)
                                if (dir.Y > 0.0001)
                                {
                                    dir = -dir;
                                }
                            }
                            else
                            {
                                // === XỬ LÝ THEO PHƯƠNG X ===
                                // Nếu đang hướng sang trái (X âm) -> Đảo ngược để luôn hướng sang phải (X dương)
                                if (dir.X < -0.0001)
                                {
                                    dir = -dir;
                                }
                            }

                            XYZ midpoint = sharedEdge.Evaluate(0.5, true);

                            BoundingBoxXYZ bbox1 = floor1.get_BoundingBox(null);
                            XYZ center1 = (bbox1.Max + bbox1.Min) / 2.0;
                            XYZ vecTo1 = center1 - midpoint;

                            double crossZ = dir.X * vecTo1.Y - dir.Y * vecTo1.X;
                            bool isFloor1Left = crossZ > 0;
                            bool isFloor1Higher = elev1 > elev2;

                            // Chọn Type dựa trên logic RL/LR
                            FamilySymbol targetSymbol = (isFloor1Left == isFloor1Higher) ? symbolRL : symbolLR;

                            View activeView = uidoc.ActiveView;
                            double targetZ = Math.Max(elev1, elev2);
                            XYZ placementPoint = new XYZ(midpoint.X, midpoint.Y, targetZ);

                            // Đặt Family
                            FamilyInstance instance = doc.Create.NewFamilyInstance(placementPoint, targetSymbol, activeView);

                            // Xoay Family
                            double angle = Math.Atan2(dir.Y, dir.X) - Math.PI / 2;
                            angle = Math.Atan2(dir.Y, dir.X) + Math.PI / 2;

                            if (Math.Abs(angle) > 0.001)
                            {
                                Line axis = Line.CreateBound(placementPoint, placementPoint + XYZ.BasisZ);
                                ElementTransformUtils.RotateElement(doc, instance.Id, axis, angle);
                            }

                            // Gán thông số StepHeight
                            double stepHeightValue = Math.Abs(elev1 - elev2) * 304.8;
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
                    TaskDialog.Show("Thông báo", "Không tìm thấy cặp sàn nào có giật cấp hoặc cạnh chung.");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }


        // Các hàm helper giữ nguyên như cũ
        private double GetFloorTopElevation(Floor floor)
        {
            Parameter p = floor.get_Parameter(BuiltInParameter.FLOOR_HEIGHTABOVELEVEL_PARAM);
            double offset = p != null ? p.AsDouble() : 0;
            Level level = floor.Document.GetElement(floor.LevelId) as Level;
            double levelElev = level != null ? level.Elevation : 0;
            return levelElev + offset;
        }

        private Line FindSharedEdgeBy2DProjection(Floor f1, Floor f2)
        {
            List<Line> edges1 = GetTopEdges(f1);
            List<Line> edges2 = GetTopEdges(f2);

            foreach (Line l1 in edges1)
            {
                foreach (Line l2 in edges2)
                {
                    Line overlap = GetOverlap2D(l1, l2);
                    if (overlap != null) return overlap;
                }
            }
            return null;
        }

        private Line GetOverlap2D(Line l1, Line l2)
        {
            XYZ p1 = new XYZ(l1.GetEndPoint(0).X, l1.GetEndPoint(0).Y, 0);
            XYZ p2 = new XYZ(l1.GetEndPoint(1).X, l1.GetEndPoint(1).Y, 0);
            XYZ q1 = new XYZ(l2.GetEndPoint(0).X, l2.GetEndPoint(0).Y, 0);
            XYZ q2 = new XYZ(l2.GetEndPoint(1).X, l2.GetEndPoint(1).Y, 0);

            XYZ v1 = (p2 - p1).Normalize();
            XYZ v2 = (q2 - q1).Normalize();
            if (!v1.IsAlmostEqualTo(v2) && !v1.IsAlmostEqualTo(-v2)) return null;

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

        private double GetProjParam(XYZ a, XYZ b, XYZ p)
        {
            XYZ ap = p - a;
            XYZ ab = b - a;
            return ap.DotProduct(ab) / ab.DotProduct(ab);
        }

        private List<Line> GetTopEdges(Floor floor)
        {
            List<Line> results = new List<Line>();
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };
            GeometryElement geo = floor.get_Geometry(opt);

            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                        {
                            foreach (EdgeArray loop in face.EdgeLoops)
                            {
                                foreach (Edge edge in loop)
                                {
                                    if (edge.AsCurve() is Line line) results.Add(line);
                                }
                            }
                        }
                    }
                }
                else if (obj is GeometryInstance inst)
                {
                    results.AddRange(GetTopEdgesFromInstance(inst));
                }
            }
            return results;
        }

        private List<Line> GetTopEdgesFromInstance(GeometryInstance inst)
        {
            List<Line> results = new List<Line>();
            foreach (GeometryObject obj in inst.GetInstanceGeometry())
            {
                if (obj is Solid solid)
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                        {
                            foreach (EdgeArray loop in face.EdgeLoops)
                            {
                                foreach (Edge edge in loop)
                                {
                                    if (edge.AsCurve() is Line line) results.Add(line);
                                }
                            }
                        }
                    }
                }
            }
            return results;
        }
    }

    public class FloorSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem) => elem is Floor;
        public bool AllowReference(Reference reference, XYZ position) => false;
    }
}
