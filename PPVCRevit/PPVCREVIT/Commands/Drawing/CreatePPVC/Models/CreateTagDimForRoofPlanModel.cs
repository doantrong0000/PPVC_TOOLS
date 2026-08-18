using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Utils;
using PPVCREVIT.Utils.FamiliesUtils;
using PPVCREVIT.Utils.Tag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateTagDimForRoofPlanModel
    {
        /// <summary>
        /// Thực hiện gắn tag sàn (WH_SlabTag_v26), tag dầm (WH_BeamTag_v26) không có leaderline và tạo các đường dim cho Roof Plan:
        /// - Dim tổng và dim các tường theo các phía (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng)
        /// - Không tạo dim shearkey (void).
        /// </summary>
        public static void CreateTagDimForRoofPlan(View view = null)
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

            // 1. Tìm hoặc load Family Symbol cho Sàn (Type THK) và Dầm
            FamilySymbol slabTagSymbol = CreateTagModel.GetSlabTagSymbol(doc, "THK");

            FamilySymbol beamTagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "WH_BeamTag_v26");
            if (beamTagSymbol == null)
            {
                beamTagSymbol = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_StructuralFramingTags)
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>()
                    .FirstOrDefault();
            }

            if (slabTagSymbol == null && beamTagSymbol == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy Family Slab Tag (WH_SlabTag_v26) hoặc Beam Tag (WH_BeamTag_v26) trong dự án.");
                return;
            }

            // Tìm hoặc load Family Symbol cho Rebar Tag
            FamilySymbol rebarTagSymbol = CreateTagModel.GetRebarTagSymbol(doc);

            // 2. Thu thập các đối tượng sàn, dầm và tường trong view hiện tại
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

            if (walls.Count == 0 && beams.Count == 0 && floors.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không tìm thấy sàn, dầm hoặc tường nào trong view hiện tại.");
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

            int slabTagCount = 0;
            int beamTagCount = 0;
            int rebarTagCount = 0;

            using (Transaction tx = new Transaction(doc, "Tự động gắn tag Sàn, Dầm và tạo dim RoofPlan"))
            {
                tx.Start();

                if (slabTagSymbol != null && !slabTagSymbol.IsActive)
                {
                    slabTagSymbol.Activate();
                }

                if (beamTagSymbol != null && !beamTagSymbol.IsActive)
                {
                    beamTagSymbol.Activate();
                }

                double midX = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.X : (wallBBox != null ? (wallBBox.Min.X + wallBBox.Max.X) / 2.0 : 0.0);
                double midY = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.Y : (wallBBox != null ? (wallBBox.Min.Y + wallBBox.Max.Y) / 2.0 : 0.0);

                // A. Gắn tag cho sàn (leaderline = false) tại tâm sàn
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
                            tag.HasLeader = false;
                            slabTagCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi khi tag sàn: {ex.Message}");
                        }
                    }
                }

                // B. Gắn tag cho dầm (leaderline = false) với offset động
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
                                    tagPos = new XYZ(center.X, center.Y + offsetVal, center.Z);
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

                        try
                        {
                            Reference hostRef = new Reference(beam);


                            IndependentTag tag = IndependentTag.Create(
                                doc,
                                beamTagSymbol.Id,
                                view.Id,
                                hostRef,
                                false,
                                TagOrientation.Horizontal,
                                tagPos
                            );
                            tag.HasLeader = false;
                            beamTagCount++;
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"Lỗi khi tag dầm: {ex.Message}");
                        }
                    }
                }

                // C. Tạo các đường Dim cho các phía của tường (Trái 2 cấp, Phải 1 cấp, Trên 1 cấp tổng)
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

                // D. Gắn tag thép (Rebar) cho từng tường nếu tìm thấy rebarTagSymbol
                if (rebarTagSymbol != null)
                {
                    if (!rebarTagSymbol.IsActive)
                    {
                        rebarTagSymbol.Activate();
                    }

                    foreach (Wall wall in walls)
                    {
                        List<Autodesk.Revit.DB.Structure.Rebar> wallRebars = visibleRebars
                            .Where(r =>
                            {
                                ElementId hostId = r.GetHostId();
                                if (hostId == wall.Id) return true;
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

                        var groupedRebars = wallRebars.GroupBy(r =>
                        {
                            Parameter rebarNumParam = r.get_Parameter(BuiltInParameter.REBAR_NUMBER);
                            return rebarNumParam?.AsString() ?? rebarNumParam?.AsValueString() ?? "";
                        });

                        foreach (var group in groupedRebars)
                        {
                            var rebar = group.FirstOrDefault();
                            if (rebar == null) continue;

                            if (rebar.IsHidden(view)) continue;

                            XYZ tagPos = GetRebarTagPosition(rebar, view);
                            if (tagPos == XYZ.Zero) continue;

                            XYZ wallCenter = GetElementCenter(wall, view);
                            double rebarOffsetVal = 1.5;

                            if (IsWallParallelToDirection(wall, XYZ.BasisX))
                            {
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
                                doc.Regenerate();
                            }

                            Reference rebarRef = RebarTagUltis.GetRebarReference(rebar, view);
                            if (rebarRef == null) continue;

                            try
                            {
                                IndependentTag tag = IndependentTag.Create(
                                    doc,
                                    rebarTagSymbol.Id,
                                    view.Id,
                                    rebarRef,
                                    true,
                                    TagOrientation.Horizontal,
                                    tagPos
                                );

                                if (tag != null)
                                {
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

            TaskDialog.Show("Kết quả", $"Đã gắn thành công:\n- {slabTagCount} tag sàn (WH_SlabTag_v26)\n- {beamTagCount} tag dầm (WH_BeamTag_v26)\n- {rebarTagCount} tag thép (category OST_RebarTags)\n\nĐã tạo dim tổng các phía và dim tường cho Roof Plan.");
        }

        private static void CreateDimensions(View view, List<Wall> walls, BoundingBoxXYZ bbox)
        {
            if (view == null || walls == null || walls.Count == 0 || bbox == null) return;
            Document doc = view.Document;

            double offsetInner = 2.0;
            double offsetOuter = 3.2;
            double buffer = 1.0;

            double minX = bbox.Min.X;
            double maxX = bbox.Max.X;
            double minY = bbox.Min.Y;
            double maxY = bbox.Max.Y;

            double midX = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.X : (minX + maxX) / 2.0;
            double midY = RevitClass.PPVCCenter != null ? RevitClass.PPVCCenter.Y : (minY + maxY) / 2.0;

            List<Element> wallElements = walls.Cast<Element>().ToList();

            // 1. TẠO DIM CẠNH TRÁI (LEFT SIDE) - Phương Y (Có 2 cấp Dim)
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

            // 2. TẠO DIM CẠNH PHẢI (RIGHT SIDE) - Phương Y (1 cấp chi tiết)
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

            // 3. TẠO DIM CẠNH TRÊN (TOP SIDE) - Phương X (1 cấp tổng)
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

        private static XYZ GetRebarTagPosition(Autodesk.Revit.DB.Structure.Rebar rebar, View view)
        {
            try
            {
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
                    IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                    if (curves == null || curves.Count == 0) return XYZ.Zero;
                    Curve firstCurve = curves[0];
                    rawCenter = (firstCurve.GetEndPoint(0) + firstCurve.GetEndPoint(1)) / 2.0;
                }

                XYZ origin = view.Origin;
                XYZ normal = view.ViewDirection;
                XYZ projected = rawCenter - normal.Multiply((rawCenter - origin).DotProduct(normal));
                return projected;
            }
            catch { }
            return XYZ.Zero;
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
