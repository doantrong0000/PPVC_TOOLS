using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Utils;
using PPVCREVIT.Utils.FamiliesUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateSlabRebarTagModel
    {
        // Từ khóa phân loại thanh trimmer (thanh đơn lẻ, không rải)
        private static readonly string[] TrimmerKeywords = { "TRIMMER" };

        // Từ khóa phân loại thanh thép sàn rải (dùng multi tag)
        private static readonly string[] SlabDistributedKeywords = { "SLAB", "LEDGE" };

        // Từ khóa phân loại thanh starter bar (dùng multi tag)
        private static readonly string[] StarterKeywords = { "START", "TIE" };

        /// <summary>
        /// Tạo tag thép cho sàn. Các thanh rebar đã được lọc từ bước filter bên ngoài (viewRebars).
        /// Phân loại tag:
        ///   - Tag đơn (IndependentTag): thanh trimmer hoặc thanh có Quantity == 1 (không rải).
        ///   - Multi tag (MultiReferenceAnnotation): thép sàn rải + starter bar có Quantity > 1.
        /// </summary>
        /// <param name="rebarTypeName">Không dùng để lọc, chỉ dùng để hiển thị kết quả</param>
        /// <param name="tagTypeName">Tên loại tag (ví dụ: "Type 3")</param>
        /// <param name="view">View 2D cần thực hiện (mặc định null -> ActiveView)</param>
        public static void CreateRebarTagForSlab(string rebarTypeName = "BOT", string tagTypeName = "Type 3", View view = null)
        {
            view = view ?? RevitClass.UiDoc.ActiveView;
            if (view == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng mở một View 2D trước khi thực hiện.");
                return;
            }

            Document doc = view.Document;

            // --- Bước 1: Lấy Symbol Tag Thép ---
            FamilySymbol rebarTagSymbol = CreateTagModel.GetRebarTagSymbol(doc, tagTypeName);
            if (rebarTagSymbol == null)
            {
                TaskDialog.Show("Lỗi", $"Không tìm thấy Family Rebar Tag phù hợp cho kiểu '{tagTypeName}' trong dự án.");
                return;
            }

            // --- Bước 2: Thu thập tất cả rebar hiển thị trong View ---
            List<Rebar> viewRebars = CollectVisibleRebars(doc, view);
            if (viewRebars.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không tìm thấy thanh thép (Rebar) nào trong View hiện tại.");
                return;
            }

            // --- Bước 3: Tính biên module ---
            BoundingBoxXYZ moduleBBox = GetModuleBoundingBox(doc, view);
            double minX = moduleBBox?.Min.X ?? 0;
            double maxX = moduleBBox?.Max.X ?? 0;
            double minY = moduleBBox?.Min.Y ?? 0;
            double maxY = moduleBBox?.Max.Y ?? 0;
            double midX = RevitClass.PPVCCenter?.X ?? (minX + maxX) / 2.0;
            double midY = RevitClass.PPVCCenter?.Y ?? (minY + maxY) / 2.0;

            int unobscuredCount = 0;
            int presentationModeCount = 0;

            using (Transaction tx = new Transaction(doc, "Tạo tag thép sàn"))
            {
                tx.Start();

                // --- Bước 4: Set Unobscured + PresentationMode cho tất cả rebar ---
                SetRebarViewProperties(view, viewRebars, ref unobscuredCount, ref presentationModeCount);

                if (!rebarTagSymbol.IsActive)
                {
                    rebarTagSymbol.Activate();
                    doc.Regenerate();
                }

                // --- Bước 5: Phân loại & gắn tag ---
                // Không lọc lại vì viewRebars đã được filter từ bước gọi bên ngoài.
                // Chỉ phân loại: trimmer/Quantity==1 → tag đơn, còn lại → multi tag.
                CreateRebarTags(doc, view, viewRebars, rebarTagSymbol,
                    minX, maxX, minY, maxY, midX, midY);

                tx.Commit();

                TaskDialog.Show("Kết quả",
                    $"Đã set Unobscured ({unobscuredCount} thanh), " +
                    $"set Presentation Mode ({presentationModeCount} thanh) và gắn tag thành công " +
                    $"cho thép sàn ({rebarTypeName} - Type: {tagTypeName}).");
            }
        }

        #region Thu thập Rebar

        /// <summary>
        /// Thu thập tất cả rebar hiển thị trong view. Fallback sang toàn Document nếu cần.
        /// </summary>
        private static List<Rebar> CollectVisibleRebars(Document doc, View view)
        {
            List<Rebar> rebars = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Rebar>()
                .ToList();

            if (rebars.Count == 0)
            {
                // Fallback: lấy từ toàn Document, lọc theo visibility trong view
                rebars = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_Rebar)
                    .WhereElementIsNotElementType()
                    .Cast<Rebar>()
                    .Where(r => !r.IsHidden(view))
                    .ToList();
            }

            return rebars;
        }

        #endregion

        #region Set thuộc tính View cho Rebar

        /// <summary>
        /// Set Unobscured và PresentationMode cho tất cả rebar trong view.
        /// PresentationMode dựa trên WH_Rebar_Type:
        ///   - SLAB / LEDGE → Middle
        ///   - TIE / START  → FirstLast
        /// </summary>
        private static void SetRebarViewProperties(View view, List<Rebar> rebars,
            ref int unobscuredCount, ref int presentationModeCount)
        {
            foreach (Rebar rebar in rebars)
            {
                // Set Unobscured
                try
                {
                    if (!rebar.IsUnobscuredInView(view))
                    {
                        rebar.SetUnobscuredInView(view, true);
                        unobscuredCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi set Unobscured cho Rebar ID {rebar.Id}: {ex.Message}");
                }

                // Set PresentationMode
                try
                {
                    string rebarType = GetRebarTypeValue(rebar);
                    if (string.IsNullOrEmpty(rebarType)) continue;

                    if (ContainsAny(rebarType, SlabDistributedKeywords))
                    {
                        rebar.SetPresentationMode(view, RebarPresentationMode.Middle);
                        presentationModeCount++;
                    }
                    else if (ContainsAny(rebarType, StarterKeywords))
                    {
                        rebar.SetPresentationMode(view, RebarPresentationMode.FirstLast);
                        presentationModeCount++;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi set PresentationMode cho Rebar ID {rebar.Id}: {ex.Message}");
                }
            }
        }

        #endregion

        #region Phân loại & Gắn Tag

        /// <summary>
        /// Phân loại rebar và gắn tag tương ứng:
        ///   - Tag đơn (IndependentTag): thanh trimmer hoặc Quantity == 1
        ///   - Multi tag (MultiReferenceAnnotation): thép sàn rải / starter bar (Quantity > 1)
        /// </summary>
        private static void CreateRebarTags(
            Document doc, View view, List<Rebar> rebars, FamilySymbol rebarTagSymbol,
            double minX, double maxX, double minY, double maxY, double midX, double midY)
        {
            // Lấy tất cả các loại MultiReferenceAnnotationType có trong dự án
            var allMraTypes = new FilteredElementCollector(doc)
                .OfClass(typeof(MultiReferenceAnnotationType))
                .Cast<MultiReferenceAnnotationType>()
                .ToList();

            // Tìm Type có tên khớp với cấu hình trong CreatePPVCConfig (ưu tiên theo thứ tự trong cấu hình)
            MultiReferenceAnnotationType mraType = null;
            if (CreatePPVCConfig.RebarTag.MRATypes != null && CreatePPVCConfig.RebarTag.MRATypes.Length > 0)
            {
                mraType = allMraTypes.FirstOrDefault(t =>
                    CreatePPVCConfig.RebarTag.MRATypes.Any(kw => t.Name.StartsWith(kw, StringComparison.OrdinalIgnoreCase))
                );
            }

            // Fallback: nếu không tìm thấy tên nào khớp, hoặc không cấu hình, lấy đại cái đầu tiên
            if (mraType == null)
            {
                mraType = allMraTypes.FirstOrDefault();
            }

            double offsetDistance = 1; // feet - khoảng cách đẩy đường dim ra ngoài biên khối

            foreach (Rebar rebar in rebars)
            {
                if (rebar.IsHidden(view)) continue;

                bool useSingleTag = IsSingleTagRebar(rebar);

                if (useSingleTag)
                {
                    CreateSingleTag(doc, view, rebar, rebarTagSymbol);
                }
                else
                {
                    // Lấy BoundingBoxXYZ của module truyền vào hàm
                    BoundingBoxXYZ moduleBox = new BoundingBoxXYZ()
                    {
                        Min = new XYZ(minX, minY, 0),
                        Max = new XYZ(maxX, maxY, 0)
                    };

                    CreateMultiTag(doc, view, rebar, rebarTagSymbol, mraType, moduleBox, offsetDistance);
                }
            }
        }

        /// <summary>
        /// Xác định thanh thép có dùng tag đơn hay không.
        /// Tag đơn khi: thanh trimmer HOẶC Quantity == 1 (thanh đơn lẻ, không rải).
        /// </summary>
        private static bool IsSingleTagRebar(Rebar rebar)
        {
            // Thanh có Quantity == 1 luôn dùng tag đơn (không phải thanh rải)
            if (rebar.Quantity == 1)
                return true;

            // Thanh trimmer luôn dùng tag đơn dù có Quantity bao nhiêu
            string rebarType = GetRebarTypeValue(rebar);
            if (!string.IsNullOrEmpty(rebarType) && ContainsAny(rebarType, TrimmerKeywords))
                return true;

            return false;
        }

        /// <summary>
        /// Tạo tag đơn (IndependentTag) cho 1 thanh thép.
        /// Dùng cho thanh trimmer và thanh có Quantity == 1.
        /// </summary>
        public static void CreateSingleTag(Document doc, View view, Rebar rebar, FamilySymbol rebarTagSymbol)
        {
            XYZ tagPos = GetRebarTagPosition(rebar, view);
            if (tagPos == XYZ.Zero) return;

            Reference rebarRef = GetRebarReference(rebar, view);
            if (rebarRef == null) return;

            try
            {
                IndependentTag tag = IndependentTag.Create(
                    doc, rebarTagSymbol.Id, view.Id, rebarRef,
                    true, // HasLeader = true
                    TagOrientation.Horizontal, tagPos);

                if (tag != null)
                {
                    tag.LeaderEndCondition = LeaderEndCondition.Free;
                    tag.TagHeadPosition = tagPos;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LỖI TAG ĐƠN] Rebar ID {rebar.Id}: {ex.Message}");
            }
        }

        /// <summary>
        /// Tạo Multi Tag (MultiReferenceAnnotation) cho thanh thép rải.
        /// Đầu vào được thu gọn: Module BoundingBox thay cho các biến min/max rời rạc.
        /// </summary>
        public static void CreateMultiTag(
            Document doc, View view, Rebar rebar, FamilySymbol rebarTagSymbol,
            MultiReferenceAnnotationType mraType,
            BoundingBoxXYZ moduleBBox, double offsetDistance = 1.0)
        {
            BoundingBoxXYZ rebarBBox = rebar.get_BoundingBox(view) ?? rebar.get_BoundingBox(null);
            if (rebarBBox == null) return;

            XYZ rebarCenter = (rebarBBox.Min + rebarBBox.Max) / 2.0;

            // Xác định hướng dim (phương rải) và vị trí tag
            CalculateDimPosition(view, rebar, rebarCenter, rebarBBox, moduleBBox, offsetDistance,
                out XYZ dimDir, out XYZ dimOrigin, out XYZ tagHeadPos);

            // Tính toán TagOrientation: song song với phương thanh thép
            // Theo yêu cầu: nếu thép phương X -> Vertical, phương Y -> Horizontal
            XYZ rebarDir = GetRebarDirection(rebar);
            XYZ rebarDirInPlane = (rebarDir - view.ViewDirection * rebarDir.DotProduct(view.ViewDirection)).Normalize();
            bool isHorizontalRebar = Math.Abs(rebarDirInPlane.X) > Math.Abs(rebarDirInPlane.Y);
            TagOrientation tagOrient = isHorizontalRebar ? TagOrientation.Vertical : TagOrientation.Horizontal;

            // Thử tạo MultiReferenceAnnotation
            bool createdMra = false;
            if (mraType != null)
            {
                try
                {
                    var options = new MultiReferenceAnnotationOptions(mraType)
                    {
                        TagHeadPosition = tagHeadPos,
                        DimensionLineDirection = dimDir,
                        DimensionLineOrigin = dimOrigin,
                        DimensionPlaneNormal = view.ViewDirection
                    };
                    options.SetElementsToDimension(new List<ElementId> { rebar.Id });

                    MultiReferenceAnnotation mra = MultiReferenceAnnotation.Create(doc, view.Id, options);
                    if (mra != null)
                    {
                        // Tắt leader line và xoay tag của MRA
                        if (mra.TagId != ElementId.InvalidElementId)
                        {
                            IndependentTag mraTag = doc.GetElement(mra.TagId) as IndependentTag;
                            if (mraTag != null)
                            {
                                mraTag.HasLeader = false;
                                try { mraTag.TagOrientation = tagOrient; } catch { }
                            }
                        }

                        // Chỉnh Equality Display của Dimension về Value
                        if (mra.DimensionId != ElementId.InvalidElementId)
                        {
                            Dimension dim = doc.GetElement(mra.DimensionId) as Dimension;
                            if (dim != null)
                            {
                                // DIM_DISPLAY_EQ là parameter để gán kieuẻ hiển thị dim
                                Parameter eqParam = dim.get_Parameter(BuiltInParameter.DIM_DISPLAY_EQ);
                                if (eqParam != null && !eqParam.IsReadOnly)
                                {
                                    eqParam.Set(0); // 0 = Value
                                }
                            }
                        }
                        createdMra = true;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi tạo MRA cho Rebar ID {rebar.Id}: {ex.Message}");
                }
            }

            // Fallback: dùng IndependentTag không leader nếu MRA thất bại
            if (!createdMra && rebarTagSymbol != null)
            {
                Reference rebarRef = GetRebarReference(rebar, view);
                if (rebarRef == null) return;

                try
                {
                    IndependentTag tag = IndependentTag.Create(
                        doc, rebarTagSymbol.Id, view.Id, rebarRef,
                        false, tagOrient, tagHeadPos);

                    if (tag != null)
                    {
                        tag.HasLeader = false;
                        tag.TagHeadPosition = tagHeadPos;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[LỖI FALLBACK TAG] Rebar ID {rebar.Id}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Tính vị trí dim line và tag head.
        /// Đã nhóm các tham số biên thành 1 biến moduleBBox duy nhất.
        /// </summary>
        private static void CalculateDimPosition(
            View view, Rebar rebar, XYZ rebarCenter, BoundingBoxXYZ rebarBBox,
            BoundingBoxXYZ moduleBBox, double offset,
            out XYZ dimDir, out XYZ dimOrigin, out XYZ tagHeadPos)
        {
            XYZ viewNormal = view.ViewDirection;

            // Lấy phương rải thép (distribution direction) — đây chính là hướng dim cần dùng
            XYZ distDir = GetDistributionDirection(rebar, viewNormal);
            dimDir = distDir;

            // Lấy phương dọc thanh thép để làm hướng đẩy dim ra ngoài biên module
            XYZ rebarDir = GetRebarDirection(rebar);
            XYZ rebarDirInPlane = rebarDir - viewNormal * rebarDir.DotProduct(viewNormal);
            if (rebarDirInPlane.GetLength() > 1e-6)
                rebarDirInPlane = rebarDirInPlane.Normalize();
            else
                rebarDirInPlane = XYZ.BasisX;

            // Trích xuất các tham số từ moduleBBox
            double minX = moduleBBox.Min.X;
            double maxX = moduleBBox.Max.X;
            double minY = moduleBBox.Min.Y;
            double maxY = moduleBBox.Max.Y;
            double midX = (minX + maxX) / 2.0;
            double midY = (minY + maxY) / 2.0;

            // Xác định chiều đẩy dim: ra xa tâm module theo phương THANH THÉP
            XYZ moduleCenter = new XYZ(midX, midY, rebarCenter.Z);
            XYZ centerToRebar = rebarCenter - moduleCenter;

            double dotSign = centerToRebar.DotProduct(rebarDirInPlane);
            double sign = dotSign >= 0 ? 1.0 : -1.0;
            XYZ pushDir = rebarDirInPlane * sign;

            // Project 4 góc module boundary lên pushDir, tìm biên xa nhất
            double maxBoundaryProj = double.MinValue;
            double[] projections = new double[]
            {
                minX * pushDir.X + minY * pushDir.Y,
                maxX * pushDir.X + minY * pushDir.Y,
                minX * pushDir.X + maxY * pushDir.Y,
                maxX * pushDir.X + maxY * pushDir.Y
            };
            foreach (double proj in projections)
            {
                if (proj > maxBoundaryProj) maxBoundaryProj = proj;
            }

            // Dim origin = tâm rebar dịch ra biên module + offset theo pushDir
            // Tọa độ dọc theo phương rải được giữ nguyên ở rebarCenter -> Tag sẽ nằm chính giữa Dim
            double rebarCenterProj = rebarCenter.X * pushDir.X + rebarCenter.Y * pushDir.Y;

            // Tính tổng khoảng cách cần đẩy (bao gồm 1.0 đơn vị dời thêm ra ngoài theo yêu cầu)
            double totalOffset = (maxBoundaryProj - rebarCenterProj) + offset + 0.5;

            XYZ rawDimOrigin = rebarCenter + pushDir * totalOffset;

            // Project các điểm lên mặt phẳng view để Revit API không bị lỗi
            dimOrigin = ProjectPointOntoViewPlane(rawDimOrigin, view);
            tagHeadPos = dimOrigin;
        }

        /// <summary>
        /// Lấy phương rải (vuông góc với phương chính thanh thép) trong mặt phẳng view.
        /// Dùng CrossProduct(viewNormal, rebarDir) để đảm bảo kết quả luôn
        /// nằm trong mặt phẳng view (orthogonal với view normal).
        /// </summary>
        private static XYZ GetDistributionDirection(Rebar rebar, XYZ viewNormal)
        {
            XYZ rebarDir = GetRebarDirection(rebar);

            // Project phương thép lên mặt phẳng view (loại bỏ thành phần Z theo view normal)
            XYZ rebarDirInPlane = rebarDir - viewNormal * rebarDir.DotProduct(viewNormal);
            if (rebarDirInPlane.GetLength() < 1e-6)
                return XYZ.BasisX; // Thanh thép song song view normal — rất hiếm

            rebarDirInPlane = rebarDirInPlane.Normalize();

            // Vuông góc trong mặt phẳng view = CrossProduct(viewNormal, rebarDirInPlane)
            // Kết quả luôn orthogonal với viewNormal → không bao giờ lỗi Revit API
            XYZ perpDir = viewNormal.CrossProduct(rebarDirInPlane);
            if (perpDir.GetLength() < 1e-6)
                return XYZ.BasisX;

            return perpDir.Normalize();
        }


        #endregion

        #region Helper: Đọc giá trị phân loại thép

        /// <summary>
        /// Đọc giá trị phân loại thép từ parameter WH_Rebar_Type.
        /// Fallback: Comments → Tên rebar.
        /// </summary>
        private static string GetRebarTypeValue(Rebar rebar)
        {
            // Ưu tiên 1: Parameter WH_Rebar_Type
            Parameter param = rebar.LookupParameter(CreatePPVCConfig.RebarTypeParamName);
            string val = param?.AsString() ?? param?.AsValueString() ?? "";

            // Ưu tiên 2: Comments
            if (string.IsNullOrEmpty(val))
            {
                Parameter commentsParam = rebar.get_Parameter(BuiltInParameter.ALL_MODEL_INSTANCE_COMMENTS);
                val = commentsParam?.AsString() ?? "";
            }

            // Ưu tiên 3: Tên rebar
            if (string.IsNullOrEmpty(val))
            {
                val = rebar.Name ?? "";
            }

            return val;
        }

        /// <summary>
        /// Kiểm tra chuỗi có chứa bất kỳ từ khóa nào không (case-insensitive).
        /// </summary>
        private static bool ContainsAny(string value, string[] keywords)
        {
            foreach (string keyword in keywords)
            {
                if (value.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        #endregion

        #region Helper: Geometry

        private static BoundingBoxXYZ GetModuleBoundingBox(Document doc, View view)
        {
            List<Wall> walls = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Walls)
                .WhereElementIsNotElementType()
                .Cast<Wall>()
                .ToList();

            BoundingBoxXYZ bbox = null;
            foreach (Wall w in walls)
            {
                BoundingBoxXYZ wBbox = w.get_BoundingBox(view) ?? w.get_BoundingBox(null);
                if (wBbox == null) continue;

                if (bbox == null)
                {
                    bbox = new BoundingBoxXYZ { Min = wBbox.Min, Max = wBbox.Max };
                }
                else
                {
                    bbox.Min = new XYZ(Math.Min(bbox.Min.X, wBbox.Min.X), Math.Min(bbox.Min.Y, wBbox.Min.Y), Math.Min(bbox.Min.Z, wBbox.Min.Z));
                    bbox.Max = new XYZ(Math.Max(bbox.Max.X, wBbox.Max.X), Math.Max(bbox.Max.Y, wBbox.Max.Y), Math.Max(bbox.Max.Z, wBbox.Max.Z));
                }
            }
            return bbox;
        }

        private static XYZ GetRebarDirection(Rebar rebar)
        {
            try
            {
                IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                if (curves != null && curves.Count > 0)
                {
                    Curve longestCurve = curves[0];
                    double maxLength = longestCurve.Length;

                    foreach (Curve curve in curves)
                    {
                        if (curve.Length > maxLength)
                        {
                            maxLength = curve.Length;
                            longestCurve = curve;
                        }
                    }

                    XYZ p0 = longestCurve.GetEndPoint(0);
                    XYZ p1 = longestCurve.GetEndPoint(1);
                    return (p1 - p0).Normalize();
                }
            }
            catch { }
            return XYZ.BasisX;
        }

        private static XYZ GetRebarTagPosition(Rebar rebar, View view)
        {
            try
            {
                BoundingBoxXYZ rebarBBox = rebar.get_BoundingBox(view) ?? rebar.get_BoundingBox(null);

                XYZ rawCenter;
                if (rebarBBox != null)
                {
                    rawCenter = (rebarBBox.Min + rebarBBox.Max) / 2.0;
                }
                else
                {
                    IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0);
                    if (curves == null || curves.Count == 0) return XYZ.Zero;

                    Curve longestCurve = curves[0];
                    double maxLength = longestCurve.Length;
                    foreach (Curve curve in curves)
                    {
                        if (curve.Length > maxLength)
                        {
                            maxLength = curve.Length;
                            longestCurve = curve;
                        }
                    }

                    rawCenter = (longestCurve.GetEndPoint(0) + longestCurve.GetEndPoint(1)) / 2.0;
                }

                return ProjectPointOntoViewPlane(rawCenter, view);
            }
            catch { }
            return XYZ.Zero;
        }

        /// <summary>
        /// Chiếu điểm 3D lên mặt phẳng của View (loại bỏ thành phần theo ViewDirection).
        /// Dùng cho tag position, dim origin, và bất kỳ điểm nào cần nằm trên view plane.
        /// </summary>
        public static XYZ ProjectPointOntoViewPlane(XYZ point, View view)
        {
            XYZ viewOrigin = view.Origin;
            XYZ viewNormal = view.ViewDirection;
            return point - viewNormal.Multiply((point - viewOrigin).DotProduct(viewNormal));
        }

        private static Reference GetRebarReference(Rebar rebar, View view)
        {
            Options opt = new Options { View = view, ComputeReferences = true };

            GeometryElement geomElem = rebar.get_Geometry(opt);
            if (geomElem != null)
            {
                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Curve curve && curve.Reference != null)
                        return curve.Reference;

                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face.Reference != null) return face.Reference;
                        }
                    }

                    if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Curve instCurve && instCurve.Reference != null)
                                return instCurve.Reference;

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

            return new Reference(rebar);
        }

        #endregion

        #region Standalone: Set Presentation Mode

        /// <summary>
        /// Set Presentation Mode cho các thanh thép hiển thị trong view:
        /// - Thanh có chứa chữ "SLAB" hoặc "LEDGE" tại WH_Rebar_Type -> RebarPresentationMode.Middle
        /// - Thanh có chứa chữ "TIE" hoặc "START" tại WH_Rebar_Type -> RebarPresentationMode.FirstLast
        /// </summary>
        public static void SetRebarPresentationModeForView(View view = null)
        {
            view = view ?? RevitClass.UiDoc.ActiveView;
            if (view == null) return;

            Document doc = view.Document;

            List<Rebar> viewRebars = new FilteredElementCollector(doc, view.Id)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Rebar>()
                .ToList();

            if (viewRebars.Count == 0) return;

            using (Transaction tx = new Transaction(doc, "Set Presentation Mode Rebar"))
            {
                tx.Start();

                foreach (Rebar rebar in viewRebars)
                {
                    try
                    {
                        string rebarType = GetRebarTypeValue(rebar);
                        if (string.IsNullOrEmpty(rebarType)) continue;

                        if (ContainsAny(rebarType, SlabDistributedKeywords))
                        {
                            rebar.SetPresentationMode(view, RebarPresentationMode.Middle);
                        }
                        else if (ContainsAny(rebarType, StarterKeywords))
                        {
                            rebar.SetPresentationMode(view, RebarPresentationMode.FirstLast);
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Lỗi set PresentationMode cho Rebar ID {rebar.Id}: {ex.Message}");
                    }
                }

                tx.Commit();
            }
        }

        #endregion
    }
}
