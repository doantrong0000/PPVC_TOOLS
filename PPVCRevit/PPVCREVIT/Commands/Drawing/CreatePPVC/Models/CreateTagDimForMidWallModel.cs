using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateTagDimForMidWallModel
    {
        /// <summary>
        /// Thực hiện gắn tag tường (WallTag) không có leaderline và tạo các đường dim:
        /// - Dim tổng và dim các tường theo các phía (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng)
        /// - Dim khoảng cách giữa các shearkey (void) cho từng tường riêng lẻ.
        /// </summary>
        public static void CreateTagDimForMidWall(View view = null)
        {
            if (view == null)
            {
                view = RevitClass.UiDoc.ActiveView;
            }

            if (view == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng mở một View 2D trước khi thực hiện.");
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

            // 1. Tìm hoặc load Family Symbol cho Wall Tag có chứa chữ WallTagKeyword
            FamilySymbol wallTagSymbol = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_WallTags)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .FirstOrDefault(fs => fs.Family.Name.IndexOf(CreatePPVCConfig.WallTagKeyword, StringComparison.OrdinalIgnoreCase) >= 0 || fs.Name.IndexOf(CreatePPVCConfig.WallTagKeyword, StringComparison.OrdinalIgnoreCase) >= 0);

            if (wallTagSymbol == null)
            {
                // Fallback: Lấy tag đầu tiên của category OST_WallTags
                wallTagSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_WallTags)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();
            }

            if (wallTagSymbol == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy Family Wall Tag nào trong dự án.");
                return;
            }

            // Tìm hoặc load Family Symbol cho Rebar Tag
            FamilySymbol rebarTagSymbol = CreateTagModel.GetRebarTagSymbol(doc);

            // 2. Thu thập các đối tượng tường trong view hiện tại
            List<Wall> walls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            if (walls.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không tìm thấy tường nào trong view hiện tại.");
                return;
            }

            // 3. Thu thập các đối tượng rebar (thép) hiển thị trong view hiện tại
            List<Autodesk.Revit.DB.Structure.Rebar> visibleRebars = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Autodesk.Revit.DB.Structure.Rebar>()
                .ToList();

            // Tính toán BoundingBox tổng thể của toàn bộ tường trong view
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

            int tagCount = 0;
            int rebarTagCount = 0;

            using (Transaction tx = new Transaction(doc, "Tự động gắn tag và tạo dim tường MillWall"))
            {
                tx.Start();

                if (!wallTagSymbol.IsActive)
                {
                    wallTagSymbol.Activate();
                }

                double midX = wallBBox != null ? (wallBBox.Min.X + wallBBox.Max.X) / 2.0 : 0.0;
                double midY = wallBBox != null ? (wallBBox.Min.Y + wallBBox.Max.Y) / 2.0 : 0.0;

                // A. Gắn tag cho tường (leaderline = false) với offset động
                foreach (Wall wall in walls)
                {
                    XYZ center = GetElementCenter(wall, view);
                    if (center == XYZ.Zero) continue;

                    XYZ tagPos = center;
                    double offsetVal = 1; // feet (tịnh tiến 1.5 feet vào phía trong)

                    if (IsWallParallelToDirection(wall, XYZ.BasisX))
                    {
                        // Tường phương X: trên thì xuống dưới (-1.5), dưới thì hướng lên trên (+1.5)
                        if (center.Y > midY)
                        {
                            tagPos = new XYZ(center.X, center.Y - offsetVal, center.Z);
                        }
                        else
                        {
                            tagPos = new XYZ(center.X, center.Y + offsetVal, center.Z);
                        }
                    }
                    else if (IsWallParallelToDirection(wall, XYZ.BasisY))
                    {
                        // Tường phương Y: tường bên phải thì tịnh tiến sang trái (-1.5), tường bên trái thì tịnh tiến sang phải (+1.5)
                        if (center.X > midX)
                        {
                            tagPos = new XYZ(center.X - offsetVal, center.Y, center.Z);
                        }
                        else
                        {
                            tagPos = new XYZ(center.X + offsetVal, center.Y, center.Z);
                        }
                    }

                    try
                    {
                        Reference hostRef = new Reference(wall);
                        IndependentTag tag = IndependentTag.Create(
                            doc,
                            wallTagSymbol.Id,
                            view.Id,
                            hostRef,
                            false,
                            TagOrientation.Horizontal,
                            tagPos
                        );
                        tag.HasLeader = false; // Tắt leaderline
                        tagCount++;
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi khi tag tường: {ex.Message}");
                    }
                }

                // B. Tạo các đường Dim cho 4 phía của tường (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng)
                if (wallBBox != null)
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

                // C. Tạo dim khoảng cách các shearkey (void) cho từng tường riêng biệt
                foreach (Wall wall in walls)
                {
                    try
                    {
                        List<Element> voids = GetWallVoids(wall, doc);
                        if (voids.Count > 0)
                        {
                            CreateShearKeyDimensionForWall(view, wall, voids, doc, midX, midY);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi khi tạo dim shearkey cho tường {wall.Id}: {ex.Message}");
                    }
                }

                // D. Gắn tag thép (Rebar) cho từng tường nếu tìm thấy rebarTagSymbol
                if (rebarTagSymbol != null)
                {
                    if (!rebarTagSymbol.IsActive)
                    {
                        rebarTagSymbol.Activate();
                    }

                    foreach (Wall wall in walls)
                    {
                        // Lọc các rebar thuộc tường này (kiểm tra cả GetHostId và Host property)
                        List<Autodesk.Revit.DB.Structure.Rebar> wallRebars = visibleRebars
                            .Where(r =>
                            {
                                ElementId hostId = r.GetHostId();
                                if (hostId == wall.Id) return true;
                                // Fallback: kiểm tra qua Host property
                                Element host = doc.GetElement(hostId);
                                if (host is Wall hostWall && hostWall.Id == wall.Id) return true;
                                return false;
                            })
                            .Where(r =>
                            {
                                Parameter param = r.LookupParameter(CreatePPVCConfig.RebarTypeParamName);
                                string val = param?.AsString() ?? param?.AsValueString() ?? "";
                                return val.Equals(CreatePPVCConfig.RebarTypeParamValue, StringComparison.OrdinalIgnoreCase);
                            })
                            .ToList();

                        // Nhóm các rebar theo Rebar Number
                        var groupedRebars = wallRebars.GroupBy(r =>
                        {
                            Parameter rebarNumParam = r.get_Parameter(BuiltInParameter.REBAR_NUMBER);
                            return rebarNumParam?.AsString() ?? rebarNumParam?.AsValueString() ?? "";
                        });

                        foreach (var group in groupedRebars)
                        {
                            var rebar = group.FirstOrDefault();
                            if (rebar == null) continue;

                            if (rebar.IsHidden(view))
                            {
                                System.Diagnostics.Debug.WriteLine($"[BỎ QUA] Thép ID {rebar.Id} đang bị ẨN trong View {view.Name}. Không thể tag!");
                                continue;
                            }

                            XYZ tagPos = GetRebarTagPosition(rebar, view);
                            if (tagPos == XYZ.Zero) continue;

                            // Tịnh tiến vị trí tag thép theo vị trí tường (1.5 feet)
                            XYZ wallCenter = GetElementCenter(wall, view);
                            double rebarOffsetVal = 1.5; // feet

                            if (IsWallParallelToDirection(wall, XYZ.BasisX))
                            {
                                // Tường phương X: trên thì xuống dưới (-1.5), dưới thì lên trên (+1.5)
                                if (wallCenter.Y > midY)
                                {
                                    tagPos = new XYZ(tagPos.X, tagPos.Y - rebarOffsetVal, tagPos.Z);
                                }
                                else
                                {
                                    tagPos = new XYZ(tagPos.X, tagPos.Y + rebarOffsetVal, tagPos.Z);
                                }
                            }
                            else if (IsWallParallelToDirection(wall, XYZ.BasisY))
                            {
                                // Tường phương Y: tường bên phải thì tịnh tiến sang trái (-1.5), tường bên trái thì tịnh tiến sang phải (+1.5)
                                if (wallCenter.X > midX)
                                {
                                    tagPos = new XYZ(tagPos.X - rebarOffsetVal, tagPos.Y, tagPos.Z);
                                }
                                else
                                {
                                    tagPos = new XYZ(tagPos.X + rebarOffsetVal + 1, tagPos.Y, tagPos.Z);
                                }
                            }

                            if (!rebarTagSymbol.IsActive)
                            {
                                rebarTagSymbol.Activate();
                                doc.Regenerate(); // Yêu cầu Revit cập nhật trạng thái Symbol
                            }

                            Reference rebarRef = GetRebarReference(rebar, view);
                            if (rebarRef == null)
                            {
                                System.Diagnostics.Debug.WriteLine($"[BỎ QUA] Không tạo được Reference cho Rebar ID {rebar.Id}");
                                continue;
                            }

                            try
                            {
                                // Tạo IndependentTag
                                IndependentTag tag = IndependentTag.Create(
                                    doc,
                                    rebarTagSymbol.Id,
                                    view.Id,
                                    rebarRef,
                                    true, // HasLeader
                                    TagOrientation.Horizontal,
                                    tagPos
                                );

                                if (tag != null)
                                {
                                    // Đặt vị trí đầu mũi tên chỉ (Leader End) vào đúng vị trí thanh thép nếu cần
                                    tag.LeaderEndCondition = LeaderEndCondition.Free;
                                    tag.TagHeadPosition = tagPos;
                                    rebarTagCount++;
                                }
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[LỖI TAG THÉP] Wall {wall.Id}, Rebar {rebar.Id}: {ex.Message}");
                            }
                        }
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Kết quả", $"Đã gắn thành công:\n- {tagCount} tag tường (leaderline = false)\n- {rebarTagCount} tag thép (category OST_RebarTags)\n\nĐã tạo dim tổng các phía, dim các tường và dim chi tiết khoảng cách shearkey.");
        }

        /// <summary>
        /// Tạo các đường Dim cho các hướng bao quanh bố cục tường (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng).
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

            List<Element> wallElements = walls.Cast<Element>().ToList();

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
                    new XYZ(minX - offsetInner, minY - buffer, 0),
                    new XYZ(minX - offsetInner, maxY + buffer, 0)
                );
                doc.Create.NewDimension(view, leftInnerLine, leftInnerArray);
            }

            // Đường dim tổng (Outer) cho bên trái - Đo từ 2 cạnh xa nhau nhất của toàn bộ Tường
            Reference southRef = GetFurthestFace(wallElements, view, XYZ.BasisY, minY, false);
            Reference northRef = GetFurthestFace(wallElements, view, XYZ.BasisY, maxY, false);
            if (southRef != null && northRef != null)
            {
                ReferenceArray leftOuterArray = new ReferenceArray();
                leftOuterArray.Append(southRef);
                leftOuterArray.Append(northRef);
                Line leftOuterLine = Line.CreateBound(
                    new XYZ(minX - offsetOuter, minY - buffer, 0),
                    new XYZ(minX - offsetOuter, maxY + buffer, 0)
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
                    new XYZ(maxX + offsetInner, minY - buffer, 0),
                    new XYZ(maxX + offsetInner, maxY + buffer, 0)
                );
                doc.Create.NewDimension(view, rightInnerLine, rightInnerArray);
            }

            // 3. TẠO DIM CẠNH TRÊN (TOP SIDE) - Phương X (Chỉ có 1 cấp tổng)
            // Lấy 2 mặt phẳng đứng xa nhất theo phương X (Đông/Tây) của toàn bộ Tường
            Reference westRef = GetFurthestFace(wallElements, view, XYZ.BasisX, minX, true);
            Reference eastRef = GetFurthestFace(wallElements, view, XYZ.BasisX, maxX, true);

            if (westRef != null && eastRef != null)
            {
                ReferenceArray topOuterArray = new ReferenceArray();
                topOuterArray.Append(westRef);
                topOuterArray.Append(eastRef);
                Line topOuterLine = Line.CreateBound(
                    new XYZ(minX - buffer, maxY + offsetOuter, 0),
                    new XYZ(maxX + buffer, maxY + offsetOuter, 0)
                );
                Dimension topOuterDim = doc.Create.NewDimension(view, topOuterLine, topOuterArray);
                if (topOuterDim != null)
                {
                    topOuterDim.Below = "TOTAL WIDTH OF MODULE";
                }
            }
        }

        /// <summary>
        /// Tạo đường dim đo khoảng cách các shearkey (void) cho một bức tường đơn lẻ.
        /// </summary>
        private static void CreateShearKeyDimensionForWall(View view, Wall wall, List<Element> voids, Document doc, double midX, double midY)
        {
            if (voids == null || voids.Count == 0) return;

            LocationCurve locCurve = wall.Location as LocationCurve;
            if (locCurve == null) return;
            Curve curve = locCurve.Curve;
            if (curve == null) return;

            XYZ p0 = curve.GetEndPoint(0);
            XYZ p1 = curve.GetEndPoint(1);
            XYZ wallDirection = (p1 - p0).Normalize();

            bool isVertical = Math.Abs(wallDirection.Y) > Math.Abs(wallDirection.X);

            // Lấy 2 cạnh ngắn ở hai đầu của bức tường làm điểm mốc bắt đầu và kết thúc đường dim
            Options opt = new Options { ComputeReferences = true, View = view };
            GeometryElement geo = wall.get_Geometry(opt);
            if (geo == null) return;

            List<(Reference Reference, double Position)> endFaces = new List<(Reference Reference, double Position)>();
            foreach (GeometryObject obj in geo)
            {
                if (obj is Solid solid && solid.Volume > 0.001)
                {
                    foreach (Face face in solid.Faces)
                    {
                        XYZ normal = face.ComputeNormal(UV.Zero).Normalize();
                        if (Math.Abs(normal.DotProduct(wallDirection)) > 0.99)
                        {
                            XYZ pt = face.Evaluate(UV.Zero);
                            double pos = isVertical ? pt.Y : pt.X;
                            endFaces.Add((face.Reference, pos));
                        }
                    }
                }
            }

            if (endFaces.Count < 2) return;

            // Sắp xếp các mặt phẳng đầu tường
            endFaces = endFaces.OrderBy(f => f.Position).ToList();
            Reference startFaceRef = endFaces.First().Reference;
            Reference endFaceRef = endFaces.Last().Reference;

            // Lấy các reference mặt phẳng chính giữa của các void
            var voidItems = new List<(Reference Reference, double Position)>();
            foreach (Element voidEl in voids)
            {
                FamilyInstance fi = voidEl as FamilyInstance;
                if (fi == null) continue;

                LocationPoint lp = fi.Location as LocationPoint;
                if (lp == null) continue;

                Reference voidRef = GetVoidCenterReference(fi, wallDirection);
                if (voidRef != null)
                {
                    double pos = isVertical ? lp.Point.Y : lp.Point.X;
                    voidItems.Add((voidRef, pos));
                }
            }

            if (voidItems.Count == 0) return;

            // Sắp xếp các void theo vị trí dọc theo chiều dài tường
            voidItems = voidItems.OrderBy(v => v.Position).ToList();

            // Tạo chuỗi reference: Mặt đầu 1 -> Các tâm void -> Mặt đầu 2
            ReferenceArray refArray = new ReferenceArray();
            refArray.Append(startFaceRef);
            foreach (var item in voidItems)
            {
                refArray.Append(item.Reference);
            }
            refArray.Append(endFaceRef);

            XYZ wallCenter = (p0 + p1) / 2.0;
            double offset = 1.5; // feet

            Line dimLine = null;
            if (isVertical)
            {
                // Kéo dim về phía ngoài (trái hoặc phải)
                double appliedOffset = (wallCenter.X < midX) ? -offset : offset;
                double dimX = wallCenter.X + appliedOffset;
                double minY = endFaces.First().Position;
                double maxY = endFaces.Last().Position;
                dimLine = Line.CreateBound(
                    new XYZ(dimX, minY, 0),
                    new XYZ(dimX, maxY, 0)
                );
            }
            else
            {
                // Kéo dim về phía ngoài (dưới hoặc trên)
                double appliedOffset = (wallCenter.Y < midY) ? -offset : offset;
                double dimY = wallCenter.Y + appliedOffset;
                double minX = endFaces.First().Position;
                double maxX = endFaces.Last().Position;
                dimLine = Line.CreateBound(
                    new XYZ(minX, dimY, 0),
                    new XYZ(maxX, dimY, 0)
                );
            }

            if (dimLine != null && refArray.Size >= 2)
            {
                doc.Create.NewDimension(view, dimLine, refArray);
            }
        }

        /// <summary>
        /// Tìm tất cả các void (shearkey) cắt qua hoặc được host bởi bức tường.
        /// </summary>
        private static List<Element> GetWallVoids(Wall wall, Document doc)
        {
            List<Element> voids = new List<Element>();

            // 1. Lấy các instance void cắt qua tường
            try
            {
                var cuttingIds = InstanceVoidCutUtils.GetCuttingVoidInstances(wall);
                foreach (ElementId id in cuttingIds)
                {
                    Element el = doc.GetElement(id);
                    if (el != null && !voids.Any(v => v.Id == id))
                    {
                        voids.Add(el);
                    }
                }
            }
            catch { }

            // 2. Lấy các family instance được host trên tường
            FilteredElementCollector hostedCollector = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilyInstance))
                .WhereElementIsNotElementType();
            foreach (FamilyInstance fi in hostedCollector.Cast<FamilyInstance>())
            {
                if (fi.Host != null && fi.Host.Id == wall.Id)
                {
                    if (!voids.Any(v => v.Id == fi.Id))
                    {
                        voids.Add(fi);
                    }
                }
            }

            return voids;
        }

        /// <summary>
        /// Lấy reference mặt phẳng tâm của FamilyInstance (void) song song/trùng hướng cần dim.
        /// </summary>
        private static Reference GetVoidCenterReference(FamilyInstance fi, XYZ wallDirection)
        {
            XYZ handDir = fi.HandOrientation;
            XYZ faceDir = fi.FacingOrientation;

            if (Math.Abs(handDir.DotProduct(wallDirection)) > 0.9)
            {
                try
                {
                    var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                    if (refs != null && refs.Count > 0) return refs[0];
                }
                catch { }
            }

            if (Math.Abs(faceDir.DotProduct(wallDirection)) > 0.9)
            {
                try
                {
                    var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                    if (refs != null && refs.Count > 0) return refs[0];
                }
                catch { }
            }

            // Fallback
            try
            {
                var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterLeftRight);
                if (refs != null && refs.Count > 0) return refs[0];
            }
            catch { }

            try
            {
                var refs = fi.GetReferences(FamilyInstanceReferenceType.CenterFrontBack);
                if (refs != null && refs.Count > 0) return refs[0];
            }
            catch { }

            return null;
        }

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

                    if (Math.Abs(wallDir.DotProduct(direction)) > 0.99)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

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

        private static List<(Reference Reference, double Position)> GetWallFacesInDirection(List<Wall> walls, View view, XYZ direction)
        {
            List<(Reference Reference, double Position)> result = new List<(Reference Reference, double Position)>();
            Options opt = new Options
            {
                ComputeReferences = true,
                View = view
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

        private static Reference GetFurthestFace(List<Element> elements, View view, XYZ normalDirection, double targetValue, bool checkX)
        {
            Reference bestRef = null;
            double minDiff = double.MaxValue;

            Options opt = new Options
            {
                ComputeReferences = true,
                View = view
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
        /// Lấy vị trí trung tâm của thanh thép để gắn tag (dựa trên trung điểm của curve centerline đầu tiên) đã chiếu lên mặt phẳng của View.
        /// </summary>
        private static XYZ GetRebarTagPosition(Autodesk.Revit.DB.Structure.Rebar rebar, View view)
        {
            try
            {
                // Ưu tiên dùng BoundingBox của rebar trong view (chính xác hơn cho rebar dạng WALL LOOP)
                BoundingBoxXYZ rebarBBox = rebar.get_BoundingBox(view);
                if (rebarBBox == null)
                {
                    rebarBBox = rebar.get_BoundingBox(null);
                }

                XYZ rawCenter;
                if (rebarBBox != null)
                {
                    rawCenter = (rebarBBox.Min + rebarBBox.Max) / 2.0;
                }
                else
                {
                    // Fallback: lấy midpoint của curve centerline đầu tiên
                    IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                    if (curves == null || curves.Count == 0) return XYZ.Zero;
                    Curve firstCurve = curves[0];
                    rawCenter = (firstCurve.GetEndPoint(0) + firstCurve.GetEndPoint(1)) / 2.0;
                }

                // Chiếu điểm lên mặt phẳng của View để tránh lệch tọa độ theo phương pháp tuyến của View
                XYZ origin = view.Origin;
                XYZ normal = view.ViewDirection;

                XYZ projected = rawCenter - normal.Multiply((rawCenter - origin).DotProduct(normal));
                return projected;
            }
            catch { }
            return XYZ.Zero;
        }

        /// <summary>
        /// Lấy Reference hợp lệ cho việc tag rebar trong view cụ thể.
        /// new Reference(rebar) không hoạt động cho rebar rải theo phương pháp tuyến của view.
        /// Cần lấy reference từ geometry thực tế của rebar trong view.
        /// </summary>
        private static Reference GetRebarReference(Rebar rebar, View view)
        {
            // Cấu hình để Revit tính toán hình học trên View hiện tại và sinh ra Reference
            Options opt = new Options();
            opt.View = view;
            opt.ComputeReferences = true; // Bắt buộc phải = true để lấy được Reference

            GeometryElement geomElem = rebar.get_Geometry(opt);
            if (geomElem != null)
            {
                foreach (GeometryObject geomObj in geomElem)
                {
                    // Trường hợp 1: Thép hiển thị dạng đường nét (Curve)
                    if (geomObj is Curve curve && curve.Reference != null)
                    {
                        return curve.Reference;
                    }
                    // Trường hợp 2: Thép hiển thị dạng khối Solid (thể tích)
                    else if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face.Reference != null) return face.Reference;
                        }
                    }
                    // Trường hợp 3: Hình học bị bọc trong GeometryInstance
                    else if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Curve instCurve && instCurve.Reference != null)
                            {
                                return instCurve.Reference;
                            }
                            if (instObj is Solid instSolid && instSolid.Faces.Size > 0)
                            {
                                foreach (Face face in instSolid.Faces)
                                {
                                    if (face.Reference != null) return face.Reference;
                                }
                            }
                        }
                    }
                }
            }

            // Nếu quét qua toàn bộ geometry không thấy, fallback về cách cũ (hoặc trả về null)
            return new Reference(rebar);
        }

    }
}
