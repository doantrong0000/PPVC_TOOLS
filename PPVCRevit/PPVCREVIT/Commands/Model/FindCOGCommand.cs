using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using JetBrains.Annotations;
using PPVCREVIT.Services;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Model
{
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class FindCOGCommand : IExternalCommand


    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Kiểm tra family COG_Marker trước
                FamilySymbol cogSymbol = FamilyLoaderService.GetCogMarkerSymbol(doc);
                if (cogSymbol == null)
                {
                    TaskDialog.Show("Thiếu Family",
                        "Không tìm thấy family 'COG_Marker' trong project.\n" +
                        "Vui lòng load file COG_Marker.rfa vào project trước.\n" +
                        "(Insert → Load Family → chọn file COG_Marker.rfa)");
                    return Result.Failed;
                }

                // 2. Lấy cấu kiện: ưu tiên selection có sẵn, nếu chưa chọn thì bắt chọn
                List<Element> selectedElements = new List<Element>();

                ICollection<ElementId> preSelected = uidoc.Selection.GetElementIds();
                if (preSelected != null && preSelected.Count > 0)
                {
                    // Đã chọn sẵn → dùng luôn
                    foreach (ElementId id in preSelected)
                    {
                        selectedElements.Add(doc.GetElement(id));
                    }
                }
                else
                {
                    // Chưa chọn → bắt chọn
                    IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, "Chọn các cấu kiện để tính trọng tâm tổng hợp");
                    foreach (Reference r in refs)
                    {
                        selectedElements.Add(doc.GetElement(r));
                    }
                }

                // 3. Tính trọng tâm chung
                XYZ globalCog = CalculateCentroidOfMultipleElements(selectedElements, doc);

                if (globalCog != null)
                {
                    // 4. Đặt marker
                    ElementId markerId;
                    using (Transaction trans = new Transaction(doc, "Đặt COG Marker"))
                    {
                        trans.Start();
                        FamilyInstance marker = doc.Create.NewFamilyInstance(globalCog, cogSymbol, StructuralType.NonStructural);
                        markerId = marker.Id;
                        trans.Commit();
                    }

                    // 5. Hiện toạ độ + ID marker (có thể copy)
                    // Convert từ internal units (feet) sang mm
                    double xMm = globalCog.X * 304.8;
                    double yMm = globalCog.Y * 304.8;
                    double zMm = globalCog.Z * 304.8;

                    TaskDialog dlg = new TaskDialog("Trọng Tâm (COG)");
                    dlg.MainInstruction = "Đã đặt COG Marker thành công";
                    dlg.MainContent =
                        $"X: {xMm:F2} mm\n" +
                        $"Y: {yMm:F2} mm\n" +
                        $"Z: {zMm:F2} mm\n\n" +
                        $"Marker ID: {markerId.Value}";
                    dlg.Show();
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
        // Giá trị mặc định (fallback) nếu không lấy được từ vật liệu
        private const double DefaultDensitySteel = 7850.0;    // kg/m³
        private const double DefaultDensityConcrete = 2400.0;  // kg/m³
        private const string CogMarkerFamilyName = "COG_Marker";

        /// <summary>
        /// Lấy khối lượng riêng (Density) từ vật liệu của cấu kiện.
        /// Trả về giá trị internal units (consistent trong cùng hệ đơn vị Revit).
        /// </summary>
        private double GetMaterialDensity(Element el, Document doc, double fallback)
        {
            try
            {
                // Lấy MaterialId từ cấu kiện (ưu tiên Structural Material)
                ElementId matId = ElementId.InvalidElementId;

                // Cách 1: Lấy từ parameter StructuralMaterialId
                Parameter matParam = el.get_Parameter(BuiltInParameter.STRUCTURAL_MATERIAL_PARAM);
                if (matParam != null && matParam.HasValue)
                    matId = matParam.AsElementId();

                // Cách 2: Nếu không có, lấy từ danh sách MaterialIds
                if (matId == ElementId.InvalidElementId)
                {
                    ICollection<ElementId> matIds = el.GetMaterialIds(false);
                    if (matIds.Count > 0)
                        matId = matIds.First();
                }

                if (matId != ElementId.InvalidElementId)
                {
                    Material material = doc.GetElement(matId) as Material;
                    if (material != null && material.StructuralAssetId != ElementId.InvalidElementId)
                    {
                        PropertySetElement pse = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                        if (pse != null)
                        {
                            StructuralAsset asset = pse.GetStructuralAsset();
                            if (asset != null && asset.Density > 0)
                                return asset.Density; // Internal units — nhất quán với Volume
                        }
                    }
                }
            }
            catch { /* Bỏ qua lỗi, dùng fallback */ }

            return fallback;
        }

        /// <summary>
        /// Lấy khối lượng riêng của thép từ RebarBarType.
        /// </summary>
        private double GetRebarDensity(Autodesk.Revit.DB.Structure.Rebar rebar, double fallback)
        {
            try
            {
                Document doc = rebar.Document;
                RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                if (barType != null)
                {
                    // Lấy MaterialId từ RebarBarType
                    Parameter matParam = barType.get_Parameter(BuiltInParameter.MATERIAL_ID_PARAM);
                    if (matParam != null && matParam.HasValue)
                    {
                        Material material = doc.GetElement(matParam.AsElementId()) as Material;
                        if (material != null && material.StructuralAssetId != ElementId.InvalidElementId)
                        {
                            PropertySetElement pse = doc.GetElement(material.StructuralAssetId) as PropertySetElement;
                            if (pse != null)
                            {
                                StructuralAsset asset = pse.GetStructuralAsset();
                                if (asset != null && asset.Density > 0)
                                    return asset.Density;
                            }
                        }
                    }
                }
            }
            catch { /* Bỏ qua lỗi, dùng fallback */ }

            return fallback;
        }

        private XYZ CalculateCentroidOfMultipleElements(List<Element> elements, Document doc)
        {
            double totalWeight = 0;
            XYZ weightedCentroidSum = XYZ.Zero;
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };

            foreach (Element el in elements)
            {
                // TRƯỜNG HỢP 1: NẾU LÀ REBAR
                if (el is Autodesk.Revit.DB.Structure.Rebar rebar)
                {
                    var rebarData = GetAbsolutePreciseRebarCentroid(rebar);

                    if (rebarData.centroid != null && rebarData.volume > 0)
                    {
                        // Lấy density từ vật liệu gán cho Rebar Type
                        double density = GetRebarDensity(rebar, DefaultDensitySteel);
                        double weight = rebarData.volume * density;
                        weightedCentroidSum += rebarData.centroid.Multiply(weight);
                        totalWeight += weight;
                    }
                }
                // TRƯỜNG HỢP 2: CẤU KIỆN CÓ SOLID (Bê tông, thép hình...)
                else
                {
                    // Lấy density từ vật liệu gán cho cấu kiện
                    double density = GetMaterialDensity(el, doc, DefaultDensityConcrete);

                    GeometryElement geoElem = el.get_Geometry(opt);
                    if (geoElem != null)
                    {
                        void ProcessGeometry(GeometryElement gElem, Transform transform)
                        {
                            foreach (GeometryObject obj in gElem)
                            {
                                if (obj is Solid solid && solid.Volume > 0.000001)
                                {
                                    double v = solid.Volume;
                                    XYZ center = transform.OfPoint(solid.ComputeCentroid());
                                    double weight = v * density;
                                    weightedCentroidSum += center.Multiply(weight);
                                    totalWeight += weight;
                                }
                                else if (obj is GeometryInstance instance)
                                {
                                    // Dùng GetSymbolGeometry + compose transform để tránh double-transform
                                    ProcessGeometry(instance.GetSymbolGeometry(), transform.Multiply(instance.Transform));
                                }
                            }
                        }
                        ProcessGeometry(geoElem, Transform.Identity);
                    }
                }
            }

            return (totalWeight > 0) ? weightedCentroidSum.Divide(totalWeight) : null;
        }
        private (XYZ centroid, double volume) GetAbsolutePreciseRebarCentroid(Autodesk.Revit.DB.Structure.Rebar rebar)
        {
            double totalVolume = 0;
            XYZ weightedCentroidSum = XYZ.Zero;

            // 1. Lấy thông tin đường kính từ Type (Thuộc tính BarDiameter)
            RebarBarType barType = rebar.Document.GetElement(rebar.GetTypeId()) as RebarBarType;
            double diameter = barType.BarModelDiameter;
            double sectionArea = Math.PI * Math.Pow(diameter / 2.0, 2);

            // 2. Lấy số lượng thanh thực tế trong bộ rải (Thuộc tính Quantity)
            int numberOfBars = rebar.Quantity;

            // 3. Duyệt qua từng "Index" của thanh thép
            for (int i = 0; i < numberOfBars; i++)
            {
                // Lấy đường tâm của riêng thanh thứ i. 
                // GetCenterlineCurves(..., i) sẽ trả về hình học chính xác của thanh đó tại vị trí đó.
                IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, i);

                double currentBarLength = 0;
                XYZ currentBarWeightedCentroid = XYZ.Zero;

                foreach (Curve curve in curves)
                {
                    double segmentLength = curve.Length;
                    XYZ segmentCentroid = XYZ.Zero;

                    if (curve is Line line)
                    {
                        segmentCentroid = (line.GetEndPoint(0) + line.GetEndPoint(1)) / 2.0;
                    }
                    else if (curve is Arc arc)
                    {
                        // Sử dụng hàm tính trọng tâm Arc chuẩn xác của bạn
                        segmentCentroid = GetArcCentroid(arc);
                    }
                    else
                    {
                        segmentCentroid = curve.Evaluate(0.5, true);
                    }

                    // Tích số (Mô-men chiều dài) của phân đoạn
                    currentBarWeightedCentroid += segmentCentroid.Multiply(segmentLength);
                    currentBarLength += segmentLength;
                }

                if (currentBarLength > 0)
                {
                    // Thể tích thực của thanh thứ i = Chiều dài thực của nó * Diện tích mặt cắt
                    double currentBarVolume = currentBarLength * sectionArea;

                    // Trọng tâm thực của riêng thanh thứ i
                    XYZ barCentroid = currentBarWeightedCentroid.Divide(currentBarLength);

                    // Cộng dồn vào tổng (Trọng số theo thể tích thực tế từng thanh)
                    weightedCentroidSum += barCentroid.Multiply(currentBarVolume);
                    totalVolume += currentBarVolume;
                }
            }

            // Kết quả là trọng tâm tổng hợp của tất cả các thanh đơn lẻ cộng lại
            if (totalVolume > 0)
            {
                XYZ finalCentroid = weightedCentroidSum.Divide(totalVolume);
                return (finalCentroid, totalVolume); // Trả về Tuple đúng kiểu khai báo
            }

            return (null, 0); // Trả về giá trị mặc định nếu không có dữ liệu
        }
        public XYZ GetArcCentroid(Arc arc)
        {
            double radius = arc.Radius;
            double arcLength = arc.Length;
            XYZ center = arc.Center;

            // 1. Kiểm tra nếu là vòng tròn khép kín (IsBound = false)
            if (!arc.IsBound)
            {
                return center;
            }

            // alpha = nửa góc ở tâm (radian)
            double alpha = (arcLength / radius) / 2.0;

            // 2. Xử lý an toàn cho góc cực nhỏ (tránh chia cho 0)
            // Theo quy tắc L'Hôpital, khi alpha -> 0 thì sin(alpha)/alpha -> 1
            if (alpha < 1e-9)
            {
                return arc.Evaluate(0.5, true); // Trọng tâm chính là điểm giữa cung
            }

            // 3. Tính hướng từ tâm đến điểm giữa cung
            XYZ midPoint = arc.Evaluate(0.5, true);
            XYZ directionVector = (midPoint - center).Normalize();

            // 4. Công thức trọng tâm cung dây mảnh: d = R * sin(a) / a
            double distanceToCentroid = radius * (Math.Sin(alpha) / alpha);

            return center + directionVector * distanceToCentroid;
        }

        /// <summary>
        /// Kiểm tra family COG_Marker đã có trong project chưa.
        /// Nếu có → trả về FamilySymbol đã activate.
        /// Nếu chưa → trả về null.
        /// </summary>
        public static FamilySymbol GetCogMarkerSymbol(Document doc)
        {
            return GetFamilySymbol(doc, CogMarkerFamilyName);
        }

        /// <summary>
        /// Tìm Family trong project theo tên và trả về FamilySymbol đầu tiên (đã activate).
        /// Trả về null nếu không tìm thấy.
        /// </summary>
        public static FamilySymbol GetFamilySymbol(Document doc, string familyName)
        {
            Family family = new FilteredElementCollector(doc)
                .OfClass(typeof(Family))
                .Cast<Family>()
                .FirstOrDefault(f => f.Name.Equals(familyName, StringComparison.OrdinalIgnoreCase));

            if (family == null)
                return null;

            ElementId symbolId = family.GetFamilySymbolIds().FirstOrDefault();
            if (symbolId == null || symbolId == ElementId.InvalidElementId)
                return null;

            FamilySymbol symbol = doc.GetElement(symbolId) as FamilySymbol;

            if (symbol != null && !symbol.IsActive)
                symbol.Activate();

            return symbol;
        }

    }


}