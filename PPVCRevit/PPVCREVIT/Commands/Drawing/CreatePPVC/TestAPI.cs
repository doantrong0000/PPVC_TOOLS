using System;
using System.Collections.Generic;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;

namespace RevitApiRebarTag
{
    [Transaction(TransactionMode.Manual)]
    public class TagAllRebarInViewCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = doc.ActiveView;

            // Kiểm tra view hiện tại có hỗ trợ tag hay không (VD: Section, FloorPlan...)
            if (activeView.ViewType == ViewType.ThreeD && !((View3D)activeView).IsLocked)
            {
                TaskDialog.Show("Lỗi View", "Không thể tag trên View 3D chưa được khóa (Locked). Vui lòng chuyển sang Section View.");
                return Result.Cancelled;
            }

            // Thu thập toàn bộ cốt thép (Rebar) đang hiển thị trong View hiện tại
            FilteredElementCollector collector = new FilteredElementCollector(doc, activeView.Id);
            IList<Element> rebars = collector
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .ToElements();

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không tìm thấy thanh thép nào trong View này.");
                return Result.Succeeded;
            }

            using (Transaction t = new Transaction(doc, "Tag All Rebars in View"))
            {
                t.Start();
                int taggedCount = 0;

                foreach (Element elem in rebars)
                {
                    Rebar rebar = elem as Rebar;
                    if (rebar == null) continue;

                    try
                    {
                        // Xác định vị trí đặt Tag (lấy tâm của BoundingBox của thanh thép)
                        BoundingBoxXYZ bbox = rebar.get_BoundingBox(activeView);
                        if (bbox == null) continue;

                        XYZ centerPoint = (bbox.Min + bbox.Max) / 2.0;
                        Reference refRebar = new Reference(rebar);

                        // Tạo Tag bằng API
                        IndependentTag newTag = IndependentTag.Create(
                            doc,
                            activeView.Id,
                          GetRebarReference(rebar, activeView),
                            false, // true = có đường gióng (leader), false = không có đường gióng
                            TagMode.TM_ADDBY_CATEGORY,
                            TagOrientation.Horizontal,
                            centerPoint
                        );

                        // Tùy chỉnh vị trí đầu tag dịch ra một chút để không đè lên thanh thép
                        // Đơn vị trong Revit là Feet, (1, 1, 0) tương đương dịch chéo ~300mm
                        newTag.TagHeadPosition = centerPoint + new XYZ(1, 1, 0);

                        taggedCount++;
                    }
                    catch (Exception ex)
                    {
                        // Bỏ qua lỗi nếu có một thanh thép cụ thể không thể tag
                        continue;
                    }
                }

                t.Commit();

                if (taggedCount > 0)
                {
                    TaskDialog.Show("Thành công", $"Đã tag thành công {taggedCount} thanh thép trong Section View.");
                }
                else
                {
                    TaskDialog.Show("Lỗi", "Không thể tạo tag. Hãy kiểm tra xem dự án đã load Rebar Tag Family chưa.");
                }
            }

            return Result.Succeeded;
        }

        private Reference GetRebarReference(Rebar rebar, View view)
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