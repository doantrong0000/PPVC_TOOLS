using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PPVCREVIT.Utils.FamiliesUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateTagDimForBaseSlabModel
    {
        /// <summary>
        /// Thực hiện chức năng tag cho sàn (WH_SlabTag_v26) và dầm (WH_BeamTag_v26) trong view chỉ định.
        /// </summary>
        /// <param name="view">View cần tạo tag, mặc định là Active View nếu truyền null.</param>
        public static void CreateTagDimForBaseSlab(View view = null)
        {
            if (view == null)
            {
                view = RevitClass.UiDoc.ActiveView;
            }

            if (view == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng mở một View 2D trước khi thực hiện gắn tag.");
                return;
            }

            // Kiểm tra xem View hiện tại có hỗ trợ gắn tag hay không
            if (view.ViewType != ViewType.FloorPlan &&
                view.ViewType != ViewType.EngineeringPlan &&
                view.ViewType != ViewType.Elevation &&
                view.ViewType != ViewType.Section &&
                view.ViewType != ViewType.DraftingView)
            {
                TaskDialog.Show("Lỗi", $"View hiện tại ({view.ViewType}) không hỗ trợ gắn tag tự động.");
                return;
            }

            Document doc = view.Document;

            // 1. Tìm hoặc load Family Symbol cho Sàn và Dầm
            FamilySymbol slabTagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "WH_SlabTag_v26");
            FamilySymbol beamTagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "WH_BeamTag_v26");

            if (slabTagSymbol == null && beamTagSymbol == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy Family WH_SlabTag_v26 hoặc WH_BeamTag_v26 trong dự án.");
                return;
            }

            // 2. Thu thập các đối tượng sàn, dầm và tường đang hiển thị trong View
            List<Element> floors = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToList();

            List<Element> beams = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .ToList();

            List<Wall> walls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            // Tính toán BoundingBox bao phủ tất cả tường để làm mốc biên định vị đường Dim
            BoundingBoxXYZ wallBBox = null;
            foreach (Wall w in walls)
            {
                BoundingBoxXYZ wBbox = w.get_BoundingBox(view);
                if (wBbox == null) wBbox = w.get_BoundingBox(null);
                if (wBbox == null) continue;

                if (wallBBox == null)
                {
                    wallBBox = new BoundingBoxXYZ { Min = wBbox.Min, Max = wBbox.Max };
                }
                else
                {
                    wallBBox.Min = new XYZ(
                        Math.Min(wallBBox.Min.X, wBbox.Min.X),
                        Math.Min(wallBBox.Min.Y, wBbox.Min.Y),
                        Math.Min(wallBBox.Min.Z, wBbox.Min.Z)
                    );
                    wallBBox.Max = new XYZ(
                        Math.Max(wallBBox.Max.X, wBbox.Max.X),
                        Math.Max(wallBBox.Max.Y, wBbox.Max.Y),
                        Math.Max(wallBBox.Max.Z, wBbox.Max.Z)
                    );
                }
            }

            double midX = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.X : (wallBBox != null ? (wallBBox.Min.X + wallBBox.Max.X) / 2.0 : 0.0);
            double midY = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.Y : (wallBBox != null ? (wallBBox.Min.Y + wallBBox.Max.Y) / 2.0 : 0.0);

            int slabTagCount = 0;
            int beamTagCount = 0;

            // 3. Thực hiện gắn tag và tạo dim trong Transaction
            using (Transaction tx = new Transaction(doc, "Tự động gắn tag và tạo dim Sàn, Dầm, Tường"))
            {
                tx.Start();

                // Kích hoạt các tag symbol nếu chưa kích hoạt
                if (slabTagSymbol != null && !slabTagSymbol.IsActive)
                {
                    slabTagSymbol.Activate();
                }
                if (beamTagSymbol != null && !beamTagSymbol.IsActive)
                {
                    beamTagSymbol.Activate();
                }

                // Gắn tag cho sàn
                if (slabTagSymbol != null)
                {
                    foreach (Element floor in floors)
                    {
                        XYZ center = GetElementCenter(floor, view);
                        if (center == XYZ.Zero) continue;

                        Reference hostRef = new Reference(floor);
                        try
                        {
                            IndependentTag tag = IndependentTag.Create(
                                doc,
                                slabTagSymbol.Id,
                                view.Id,
                                hostRef,
                                false,
                                TagOrientation.Horizontal,
                                center
                            );
                            slabTagCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi khi tag sàn: {ex.Message}");
                        }
                    }
                }

                // Gắn tag cho dầm
                if (beamTagSymbol != null)
                {
                    foreach (Element beam in beams)
                    {
                        XYZ center = GetElementCenter(beam, view);
                        if (center == XYZ.Zero) continue;

                        XYZ tagPos = center;
                        FamilyInstance fiBeam = beam as FamilyInstance;
                        if (fiBeam != null)
                        {
                            double offsetVal = 1.4; // feet
                            if (IsBeamParallelToDirection(fiBeam, XYZ.BasisX))
                            {
                                // Dầm phương X: tag đẩy lên trên nếu ở nửa trên, đẩy xuống dưới nếu ở nửa dưới
                                if (center.Y > midY)
                                {
                                    tagPos = new XYZ(center.X, center.Y - offsetVal, center.Z);
                                }
                                else
                                {
                                    tagPos = new XYZ(center.X, center.Y - offsetVal, center.Z);
                                }
                            }
                            else if (IsBeamParallelToDirection(fiBeam, XYZ.BasisY))
                            {
                                // Dầm phương Y: tag đẩy sang phải nếu ở nửa phải, đẩy sang trái nếu ở nửa trái
                                if (center.X > midX)
                                {
                                    tagPos = new XYZ(center.X + offsetVal, center.Y, center.Z);
                                }
                                else
                                {
                                    tagPos = new XYZ(center.X - offsetVal, center.Y, center.Z);
                                }
                            }
                        }

                        Reference hostRef = new Reference(beam);
                        try
                        {
                            IndependentTag tag = IndependentTag.Create(
                                doc,
                                beamTagSymbol.Id,
                                view.Id,
                                hostRef,
                                false,
                                TagOrientation.Horizontal,
                                tagPos
                            );
                            beamTagCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi khi tag dầm: {ex.Message}");
                        }
                    }
                }

                // Tạo các đường Dim cho 4 phía của tường bao quanh PPVC
                if (walls.Count > 0 && wallBBox != null)
                {
                    try
                    {
                        CreateDimensions(view, walls, wallBBox);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi khi tạo dim cho tường: {ex.Message}");
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Kết quả", $"Đã gắn thành công:\n- {slabTagCount} tag sàn (WH_SlabTag_v26)\n- {beamTagCount} tag dầm (WH_BeamTag_v26)\n\nĐã tạo các đường dim chi tiết và tổng thể cho 4 phía tường PPVC.");
        }

        /// <summary>
        /// Tạo các đường Dim cho các hướng bao quanh khối PPVC (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng).
        /// </summary>
        private static void CreateDimensions(View view, List<Wall> walls, BoundingBoxXYZ bbox)
        {
            if (view == null || walls == null || walls.Count == 0 || bbox == null) return;
            Document doc = view.Document;

            double offsetInner = 2.0; // Khoảng cách từ biên tường đến đường dim chi tiết (feet)
            double offsetOuter = 3.2; // Khoảng cách từ biên tường đến đường dim tổng (feet)
            double buffer = 1.0;      // Độ vươn biên của đường thẳng dim (feet)

            double minX = bbox.Min.X;
            double maxX = bbox.Max.X;
            double minY = bbox.Min.Y;
            double maxY = bbox.Max.Y;

            double midX = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.X : (minX + maxX) / 2.0;
            double midY = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.Y : (minY + maxY) / 2.0;

            // Thu thập sàn và dầm trong view để tính bounding box tổng thể cho cả module
            List<Element> floors = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Floors)
                .WhereElementIsNotElementType()
                .ToList();

            List<Element> beams = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_StructuralFraming)
                .WhereElementIsNotElementType()
                .ToList();

            List<Element> allModuleElements = floors.Concat(beams).Concat(walls.Cast<Element>()).ToList();

            // Tính BoundingBox tổng thể của cả module (sàn + dầm + tường)
            BoundingBoxXYZ moduleBBox = null;
            foreach (Element el in allModuleElements)
            {
                BoundingBoxXYZ elBbox = el.get_BoundingBox(view);
                if (elBbox == null) elBbox = el.get_BoundingBox(null);
                if (elBbox == null) continue;

                if (moduleBBox == null)
                {
                    moduleBBox = new BoundingBoxXYZ { Min = elBbox.Min, Max = elBbox.Max };
                }
                else
                {
                    moduleBBox.Min = new XYZ(
                        Math.Min(moduleBBox.Min.X, elBbox.Min.X),
                        Math.Min(moduleBBox.Min.Y, elBbox.Min.Y),
                        Math.Min(moduleBBox.Min.Z, elBbox.Min.Z)
                    );
                    moduleBBox.Max = new XYZ(
                        Math.Max(moduleBBox.Max.X, elBbox.Max.X),
                        Math.Max(moduleBBox.Max.Y, elBbox.Max.Y),
                        Math.Max(moduleBBox.Max.Z, elBbox.Max.Z)
                    );
                }
            }

            double modMinX = moduleBBox != null ? moduleBBox.Min.X : minX;
            double modMaxX = moduleBBox != null ? moduleBBox.Max.X : maxX;
            double modMinY = moduleBBox != null ? moduleBBox.Min.Y : minY;
            double modMaxY = moduleBBox != null ? moduleBBox.Max.Y : maxY;

            // 1. TẠO DIM CẠNH TRÁI (LEFT SIDE) - Phương Y (Có 2 cấp Dim)
            // Lấy các tường song song phương Y và nằm ở nửa bên trái
            List<Wall> leftYWalls = walls
                .Where(w => IsWallParallelToDirection(w, XYZ.BasisY))
                .Where(w =>
                {
                    XYZ center = GetElementCenter(w, view);
                    return center != XYZ.Zero && center.X < midX;
                })
                .ToList();

            List<(Reference Reference, double Position)> leftYFacesRaw = GetWallFacesInDirection(leftYWalls, view, XYZ.BasisY);
            var leftYFacesUnique = GroupAndSortFaces(leftYFacesRaw);

            if (leftYFacesUnique.Count >= 2)
            {
                // Đường dim chi tiết (Inner)
                ReferenceArray leftInnerArray = new ReferenceArray();
                foreach (var face in leftYFacesUnique)
                {
                    leftInnerArray.Append(face.Reference);
                }
                Line leftInnerLine = Line.CreateBound(
                    new XYZ(modMinX - offsetInner, modMinY - buffer, 0),
                    new XYZ(modMinX - offsetInner, modMaxY + buffer, 0)
                );
                doc.Create.NewDimension(view, leftInnerLine, leftInnerArray);
            }

            // Đường dim tổng (Outer) cho bên trái - Đo từ 2 cạnh xa nhau nhất của toàn bộ Module
            Reference southRef = GetFurthestFace(allModuleElements, view, XYZ.BasisY, modMinY, false);
            Reference northRef = GetFurthestFace(allModuleElements, view, XYZ.BasisY, modMaxY, false);
            if (southRef != null && northRef != null)
            {
                ReferenceArray leftOuterArray = new ReferenceArray();
                leftOuterArray.Append(southRef);
                leftOuterArray.Append(northRef);
                Line leftOuterLine = Line.CreateBound(
                    new XYZ(modMinX - offsetOuter, modMinY - buffer, 0),
                    new XYZ(modMinX - offsetOuter, modMaxY + buffer, 0)
                );
                Dimension leftOuterDim = doc.Create.NewDimension(view, leftOuterLine, leftOuterArray);
                if (leftOuterDim != null)
                {
                    leftOuterDim.Below = "TOTAL LENGTH OF MODULE";
                }
            }

            // 2. TẠO DIM CẠNH PHẢI (RIGHT SIDE) - Phương Y (Chỉ có 1 cấp chi tiết)
            // Lấy các tường song song phương Y và nằm ở nửa bên phải
            List<Wall> rightYWalls = walls
                .Where(w => IsWallParallelToDirection(w, XYZ.BasisY))
                .Where(w =>
                {
                    XYZ center = GetElementCenter(w, view);
                    return center != XYZ.Zero && center.X >= midX;
                })
                .ToList();

            List<(Reference Reference, double Position)> rightYFacesRaw = GetWallFacesInDirection(rightYWalls, view, XYZ.BasisY);
            var rightYFacesUnique = GroupAndSortFaces(rightYFacesRaw);

            if (rightYFacesUnique.Count >= 2)
            {
                // Đường dim chi tiết (Inner)
                ReferenceArray rightInnerArray = new ReferenceArray();
                foreach (var face in rightYFacesUnique)
                {
                    rightInnerArray.Append(face.Reference);
                }
                Line rightInnerLine = Line.CreateBound(
                    new XYZ(modMaxX + offsetInner, modMinY - buffer, 0),
                    new XYZ(modMaxX + offsetInner, modMaxY + buffer, 0)
                );
                doc.Create.NewDimension(view, rightInnerLine, rightInnerArray);
            }

            // 3. TẠO DIM CẠNH TRÊN (TOP SIDE) - Phương X (Chỉ có 1 cấp tổng)
            // Lấy 2 mặt phẳng đứng xa nhất theo phương X (Đông/Tây) của toàn bộ Module
            Reference westRef = GetFurthestFace(allModuleElements, view, XYZ.BasisX, modMinX, true);
            Reference eastRef = GetFurthestFace(allModuleElements, view, XYZ.BasisX, modMaxX, true);

            if (westRef != null && eastRef != null)
            {
                ReferenceArray topOuterArray = new ReferenceArray();
                topOuterArray.Append(westRef);
                topOuterArray.Append(eastRef);
                Line topOuterLine = Line.CreateBound(
                    new XYZ(modMinX - buffer, modMaxY + offsetOuter, 0),
                    new XYZ(modMaxX + buffer, modMaxY + offsetOuter, 0)
                );
                Dimension topOuterDim = doc.Create.NewDimension(view, topOuterLine, topOuterArray);
                if (topOuterDim != null)
                {
                    topOuterDim.Below = "TOTAL WIDTH OF MODULE";
                }
            }
        }

        /// <summary>
        /// Kiểm tra tim tường (Center Line) có song song với hướng chỉ định hay không.
        /// </summary>
        private static bool IsWallParallelToDirection(Wall wall, XYZ direction)
        {
            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve != null)
            {
                Curve curve = locCurve.Curve;
                if (curve != null)
                {
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    XYZ wallDir = (p1 - p0).Normalize();

                    // Hai vector song song khi tích vô hướng tuyệt đối xấp xỉ 1
                    if (Math.Abs(wallDir.DotProduct(direction)) > 0.99)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Nhóm các mặt phẳng trùng nhau theo tọa độ (dung sai 0.01 feet ~ 3mm) và sắp xếp tăng dần.
        /// </summary>
        private static List<(Reference Reference, double Position)> GroupAndSortFaces(List<(Reference Reference, double Position)> rawFaces)
        {
            var uniqueFaces = new List<(Reference Reference, double Position)>();
            foreach (var f in rawFaces.OrderBy(x => x.Position))
            {
                if (!uniqueFaces.Any(uf => Math.Abs(uf.Position - f.Position) < 0.01))
                {
                    uniqueFaces.Add(f);
                }
            }
            return uniqueFaces;
        }

        /// <summary>
        /// Lọc tất cả các mặt phẳng của tường song song với hướng vector chỉ định và lấy tọa độ chiếu của nó.
        /// </summary>
        private static List<(Reference Reference, double Position)> GetWallFacesInDirection(List<Wall> walls, View view, XYZ direction)
        {
            List<(Reference Reference, double Position)> result = new List<(Reference Reference, double Position)>();
            Options opt = new Options
            {
                ComputeReferences = true, // Bắt buộc phải có để face.Reference không bị null
                View = view               // Lấy hình học cắt qua View hiện tại (Revit tự động dùng DetailLevel của View)
            };

            foreach (Wall wall in walls)
            {
                GeometryElement geo = wall.get_Geometry(opt);
                if (geo == null) continue;

                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid solid && solid.Volume > 0.001)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            XYZ normal = face.ComputeNormal(UV.Zero).Normalize();
                            // Kiểm tra vector pháp tuyến của mặt phẳng có trùng hướng với hướng cần dim không
                            if (Math.Abs(normal.DotProduct(direction)) > 0.99)
                            {
                                double pos = face.Evaluate(UV.Zero).DotProduct(direction);
                                result.Add((face.Reference, pos));
                            }
                        }
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Lấy tọa độ trung tâm của cấu kiện trong View chỉ định.
        /// </summary>
        private static XYZ GetElementCenter(Element el, View view)
        {
            BoundingBoxXYZ bbox = el.get_BoundingBox(view);
            if (bbox == null)
            {
                bbox = el.get_BoundingBox(null);
            }
            if (bbox != null)
            {
                return (bbox.Min + bbox.Max) / 2.0;
            }
            return XYZ.Zero;
        }

        /// <summary>
        /// Tìm mặt phẳng ngoài cùng của danh sách cấu kiện theo phương và tọa độ mục tiêu.
        /// </summary>
        private static Reference GetFurthestFace(List<Element> elements, View view, XYZ normalDirection, double targetValue, bool checkX)
        {
            Reference bestRef = null;
            double minDiff = double.MaxValue;

            Options opt = new Options
            {
                ComputeReferences = true,
                View = view               // Lấy hình học cắt qua View hiện tại (đáp ứng đúng Revit API khi view được chỉ định)
            };

            foreach (Element el in elements)
            {
                GeometryElement geo = el.get_Geometry(opt);
                if (geo == null) continue;

                foreach (GeometryObject obj in geo)
                {
                    if (obj is Solid solid && solid.Volume > 0.001)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            XYZ normal = face.ComputeNormal(UV.Zero).Normalize();
                            // Kiểm tra pháp tuyến có song song/trùng với hướng chỉ định không
                            if (Math.Abs(normal.DotProduct(normalDirection)) > 0.99)
                            {
                                XYZ pt = face.Evaluate(UV.Zero);
                                double val = checkX ? pt.X : pt.Y;
                                double diff = Math.Abs(val - targetValue);
                                if (diff < minDiff)
                                {
                                    minDiff = diff;
                                    bestRef = face.Reference;
                                }
                            }
                        }
                    }
                }
            }
            return bestRef;
        }

        /// <summary>
        /// Kiểm tra tim dầm (Center Line) có song song với hướng chỉ định hay không.
        /// </summary>
        private static bool IsBeamParallelToDirection(FamilyInstance beam, XYZ direction)
        {
            if (beam == null) return false;
            LocationCurve locCurve = beam.Location as LocationCurve;
            if (locCurve != null)
            {
                Curve curve = locCurve.Curve;
                if (curve != null)
                {
                    XYZ p0 = curve.GetEndPoint(0);
                    XYZ p1 = curve.GetEndPoint(1);
                    XYZ beamDir = (p1 - p0).Normalize();

                    // Hai vector song song khi tích vô hướng tuyệt đối xấp xỉ 1
                    if (Math.Abs(beamDir.DotProduct(direction)) > 0.99)
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
