using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Model
{
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
                FamilySymbol cogSymbol = GetCogMarkerSymbol(doc);
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
                    IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, "Chọn các cấu kiện để tính trọng tâm (không tính thép)");
                    foreach (Reference r in refs)
                    {
                        selectedElements.Add(doc.GetElement(r));
                    }
                }

                // 3. Tính trọng tâm chung (không dùng thép)
                XYZ globalCog = CalculateCentroidOfMultipleElements(selectedElements, doc);

                if (globalCog != null)
                {
                    // 4. Đặt marker
                    ElementId markerId;
                    using (Transaction trans = new Transaction(doc, "Đặt COG Marker (Không Thép)"))
                    {
                        trans.Start();
                        FamilyInstance marker = doc.Create.NewFamilyInstance(globalCog, cogSymbol, Autodesk.Revit.DB.Structure.StructuralType.NonStructural);
                        markerId = marker.Id;
                        trans.Commit();
                    }

                    // 5. Hiện toạ độ + ID marker (có thể copy)
                    // Convert từ internal units (feet) sang mm
                    double xMm = globalCog.X * 304.8;
                    double yMm = globalCog.Y * 304.8;
                    double zMm = globalCog.Z * 304.8;

                    TaskDialog dlg = new TaskDialog("Trọng Tâm Không Thép (COG)");
                    dlg.MainInstruction = "Đã đặt COG Marker (Không Thép) thành công";
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

        private XYZ CalculateCentroidOfMultipleElements(List<Element> elements, Document doc)
        {
            double totalWeight = 0;
            XYZ weightedCentroidSum = XYZ.Zero;
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };

            foreach (Element el in elements)
            {
                // Bỏ qua nếu là Rebar (Thép)
                if (el is Autodesk.Revit.DB.Structure.Rebar)
                {
                    continue;
                }

                // CẤU KIỆN CÓ SOLID (Bê tông, thép hình...)
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

            return (totalWeight > 0) ? weightedCentroidSum.Divide(totalWeight) : null;
        }

        /// <summary>
        /// Lấy hoặc load family COG_Marker và trả về FamilySymbol đã activate.
        /// </summary>
        public static FamilySymbol GetCogMarkerSymbol(Document doc)
        {
            return PPVCREVIT.Utils.FamiliesUtils.LoadFamilyUtils.GetFamilySymbol(doc, CogMarkerFamilyName);
        }
    }
}
