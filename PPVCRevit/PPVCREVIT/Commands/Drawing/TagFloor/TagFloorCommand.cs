using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Commands.Drawing.TagFloor.Model;
using PPVCREVIT.Utils.FamiliesUtils;
using System;
using System.Collections.Generic;
using System.Linq;
using static PPVCREVIT.Utils.Filters.FloorFilters;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class TagFloorCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

            try
            {
                FamilySymbol tagSymbol = LoadFamilyUtils.GetFamilySymbol(doc, "SlabThicknessTag");

                if (tagSymbol == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy Family 'SlabThicknessTag' trong dự án. Vui lòng load family này trước.");
                    return Result.Failed;
                }

                IList<Reference> selectedRefs;
                try
                {
                    // Quét chọn sàn trực tiếp ở Host (cho phép quét chọn bằng khung)
                    selectedRefs = uidoc.Selection.PickObjects(
                        ObjectType.Element,
                        new LocalFloorSelectionFilter(),
                        "Quét chọn các sàn cần gắn tag chiều dày"
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
                using (Transaction tx = new Transaction(doc, "Gắn tag chiều dày sàn (Host)"))
                {
                    tx.Start();

                    if (!tagSymbol.IsActive)
                    {
                        tagSymbol.Activate();
                    }

                    // Loại bỏ trùng lặp
                    var uniqueRefs = selectedRefs.GroupBy(x => x.ElementId).Select(g => g.First()).ToList();

                    foreach (Reference r in uniqueRefs)
                    {
                        Floor floor = doc.GetElement(r) as Floor;
                        if (floor != null)
                        {
                            TagFloorModel.ProcessFloor(doc, floor, Transform.Identity, tagSymbol, activeView, ref tagCount);
                        }
                    }

                    tx.Commit();
                }

                TaskDialog.Show("Thành công", $"Đã gắn {tagCount} tag chiều dày sàn.");
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