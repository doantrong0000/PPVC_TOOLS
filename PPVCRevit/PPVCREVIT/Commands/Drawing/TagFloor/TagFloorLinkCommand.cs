using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Commands.Drawing.TagFloor.Model;
using PPVCREVIT.Utils.FamiliesUtils;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class TagFloorLinkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            try
            {
                FamilySymbol tagSymbol = LoadFamilyUtils.GetOrLoadFamilySymbol(doc, "SlabThicknessTag");

                if (tagSymbol == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy Family 'SlabThicknessTag' trong dự án. Vui lòng load family này trước.");
                    return Result.Failed;
                }

                IList<Reference> selectedRefs;
                try
                {
                    // Chọn sàn trong file Link (chọn từng cấu kiện)
                    selectedRefs = uidoc.Selection.PickObjects(
                        ObjectType.LinkedElement, 
                        new LinkFloorSelectionFilter(doc), 
                        "Chọn các sàn trong file Link để gắn tag chiều dày"
                    );
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (selectedRefs == null || selectedRefs.Count == 0)
                {
                    return Result.Cancelled;
                }

                int tagCount = 0;
                using (Transaction tx = new Transaction(doc, "Gắn tag chiều dày sàn (Link)"))
                {
                    tx.Start();

                    if (!tagSymbol.IsActive)
                    {
                        tagSymbol.Activate();
                    }

                    // Loại bỏ trùng lặp dựa trên ID cấu kiện liên kết
                    var uniqueRefs = selectedRefs
                        .GroupBy(r => $"{r.ElementId}_{r.LinkedElementId}")
                        .Select(g => g.First())
                        .ToList();

                    foreach (Reference r in uniqueRefs)
                    {
                        if (r.LinkedElementId != ElementId.InvalidElementId)
                        {
                            RevitLinkInstance linkInst = doc.GetElement(r.ElementId) as RevitLinkInstance;
                            if (linkInst != null)
                            {
                                Document linkDoc = linkInst.GetLinkDocument();
                                if (linkDoc != null)
                                {
                                    Floor linkedFloor = linkDoc.GetElement(r.LinkedElementId) as Floor;
                                    if (linkedFloor != null)
                                    {
                                        Transform transform = linkInst.GetTotalTransform();
                                        TagFloorModel.ProcessFloor(doc, linkedFloor, transform, tagSymbol, activeView, ref tagCount);
                                    }
                                }
                            }
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Thành công", $"Đã gắn {tagCount} tag chiều dày sàn từ file Link.");
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Hệ Thống", ex.ToString());
                return Result.Failed;
            }

            return Result.Succeeded;
        }

    }
}
