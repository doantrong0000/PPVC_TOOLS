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
    public class GetVolumeFromLinkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                // 1. Yêu cầu người dùng chọn Revit Link
                Reference linkRef = uidoc.Selection.PickObject(ObjectType.Element, new LinkSelectionFilter(), "Chọn file Revit Link");
                RevitLinkInstance linkInstance = doc.GetElement(linkRef) as RevitLinkInstance;

                if (linkInstance == null) return Result.Cancelled;

                // 2. Lấy Document của file Link
                Document linkDoc = linkInstance.GetLinkDocument();
                if (linkDoc == null)
                {
                    TaskDialog.Show("Lỗi", "File Link chưa được tải (Loaded).");
                    return Result.Failed;
                }

                // 3. Tìm tất cả các vật liệu Bê tông (Concrete) trong file Link
                // Dựa vào Class vật liệu là "Concrete" hoặc Tên có chứa chữ "Concrete", "Bê tông"
                List<ElementId> concreteMatIds = new List<ElementId>();
                FilteredElementCollector matCollector = new FilteredElementCollector(linkDoc).OfClass(typeof(Material));

                foreach (Material mat in matCollector)
                {
                    bool isConcrete = (mat.MaterialClass != null && mat.MaterialClass.Equals("Concrete", StringComparison.OrdinalIgnoreCase)) ||
                                      mat.Name.IndexOf("Concrete", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      mat.Name.IndexOf("Bê tông", StringComparison.OrdinalIgnoreCase) >= 0;

                    if (isConcrete)
                    {
                        concreteMatIds.Add(mat.Id);
                    }
                }

                if (concreteMatIds.Count == 0)
                {
                    TaskDialog.Show("Thông báo", "Không tìm thấy vật liệu Bê tông (Concrete) nào trong file Link.");
                    return Result.Succeeded;
                }

                // 4. Lấy TẤT CẢ các cấu kiện 3D (Model Elements) trong link
                FilteredElementCollector collector = new FilteredElementCollector(linkDoc)
                    .WhereElementIsNotElementType()
                    .WhereElementIsViewIndependent();

                double totalVolumeCubicFeet = 0;

                // 5. Duyệt qua toàn bộ cấu kiện và lấy thể tích của riêng lớp vật liệu Bê tông
                foreach (Element elem in collector)
                {
                    // Chỉ xét các cấu kiện thuộc Model (bỏ qua annotation, tag, line...)
                    if (elem.Category != null && elem.Category.CategoryType == CategoryType.Model)
                    {
                        foreach (ElementId matId in concreteMatIds)
                        {
                            // Hàm này trả về thể tích chính xác của vật liệu trong cấu kiện (đã trừ giao cắt)
                            double matVol = elem.GetMaterialVolume(matId);
                            if (matVol > 0)
                            {
                                totalVolumeCubicFeet += matVol;
                            }
                        }
                    }
                }

                // 6. Chuyển đổi từ Cubic Feet sang Cubic Meters (m3)
                double conversionFactor = 0.028316846592;
                double totalVolumeM3 = Math.Round(totalVolumeCubicFeet * conversionFactor, 4);

                // 7. Hiển thị kết quả
                TaskDialog.Show("Kết quả tính thể tích", $"Tổng thể tích mọi vật liệu Bê tông trong Link:\n\n{totalVolumeM3} m³");

                return Result.Succeeded;
            }
            catch (Autodesk.Revit.Exceptions.OperationCanceledException)
            {
                // Người dùng nhấn ESC để hủy lệnh
                return Result.Cancelled;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi", ex.Message);
                return Result.Failed;
            }
        }
    }

    // Class phụ: Lọc đối tượng trong màn hình để chỉ cho phép click vào Revit Link
    public class LinkSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem is RevitLinkInstance;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            return false;
        }
    }
}