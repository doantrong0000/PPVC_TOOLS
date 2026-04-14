using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using JetBrains.Annotations;
using System;
using System.Collections.Generic;

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
                // 1. Cho phép chọn nhiều cấu kiện
                IList<Reference> refs = uidoc.Selection.PickObjects(ObjectType.Element, "Chọn các cấu kiện để tính trọng tâm tổng hợp");

                List<Element> selectedElements = new List<Element>();
                foreach (Reference r in refs)
                {
                    selectedElements.Add(doc.GetElement(r));
                }

                // 2. Tính trọng tâm chung
                XYZ globalCog = CalculateCentroidOfMultipleElements(selectedElements);

                if (globalCog != null)
                {
                    using (Transaction trans = new Transaction(doc, "Đặt Marker Tổng"))
                    {
                        trans.Start();
                        // Sử dụng hàm tạo khối trụ đã sửa ở câu trước
                        CreateCylinderMarker(doc, globalCog, 0.3);
                        trans.Commit();
                    }

                    TaskDialog.Show("Kết quả", "Đã tính xong trọng tâm cho " + selectedElements.Count + " cấu kiện.");
                }

                return Result.Succeeded;
            }
            catch (OperationCanceledException) { return Result.Cancelled; }
            catch (Exception ex) { message = ex.Message; return Result.Failed; }
        }
        private XYZ CalculateCentroidOfMultipleElements(List<Element> elements)
        {
            double totalVolume = 0;
            XYZ weightedCentroidSum = XYZ.Zero;
            Options opt = new Options { DetailLevel = ViewDetailLevel.Fine };

            // Hàm đệ quy quét Solid (giữ nguyên logic cũ nhưng để bên trong hoặc ngoài tùy bạn)
            void ProcessGeometry(GeometryElement gElem, Transform transform)
            {
                foreach (GeometryObject obj in gElem)
                {
                    if (obj is Solid solid && solid.Volume > 0.000001)
                    {
                        double v = solid.Volume;
                        XYZ center = transform.OfPoint(solid.ComputeCentroid());

                        weightedCentroidSum += center.Multiply(v); // Cộng dồn mô-men thể tích
                        totalVolume += v; // Cộng dồn tổng thể tích
                    }
                    else if (obj is GeometryInstance instance)
                    {
                        // Lưu ý: Dùng GetInstanceGeometry() để lấy đúng vị trí trong Project
                        ProcessGeometry(instance.GetInstanceGeometry(), instance.Transform);
                    }
                }
            }

            // Duyệt qua từng Element trong danh sách được chọn
            foreach (Element el in elements)
            {
                GeometryElement geoElem = el.get_Geometry(opt);
                if (geoElem != null)
                {
                    ProcessGeometry(geoElem, Transform.Identity);
                }
            }

            // Kết quả cuối cùng là trọng tâm của cả cụm cấu kiện
            return (totalVolume > 0) ? weightedCentroidSum.Divide(totalVolume) : null;
        }



        private void CreateCylinderMarker(Document doc, XYZ center, double radius)
        {
            // 1. Tạo một đường tròn nằm trên mặt phẳng XY tại vị trí Center
            // Chúng ta lùi cao độ xuống một chút để Center nằm chính giữa khối trụ
            double halfHeight = radius * 2;
            XYZ bottomCenter = center - XYZ.BasisZ * halfHeight;

            Plane plane = Plane.CreateByNormalAndOrigin(XYZ.BasisZ, bottomCenter);

            // Tạo 2 cung tròn ghép lại thành đường tròn (Revit không cho tạo 1 cung 360 độ trực tiếp trong CurveLoop)
            Arc arc1 = Arc.Create(plane, radius, 0, Math.PI);
            Arc arc2 = Arc.Create(plane, radius, Math.PI, 2 * Math.PI);

            CurveLoop profile = new CurveLoop();
            profile.Append(arc1);
            profile.Append(arc2);

            List<CurveLoop> profileLoops = new List<CurveLoop> { profile };

            // 2. Tạo khối trụ bằng cách Extrude (Đùn) đường tròn lên theo trục Z
            // Độ cao là halfHeight * 2 để Center nằm ở giữa khối trụ
            Solid cylinder = GeometryCreationUtilities.CreateExtrusionGeometry(profileLoops, XYZ.BasisZ, halfHeight * 2);

            // 3. Tạo DirectShape để hiển thị trong môi trường 3D
            DirectShape ds = DirectShape.CreateElement(doc, new ElementId(BuiltInCategory.OST_GenericModel));
            ds.ApplicationId = "PPVCREVIT";
            ds.ApplicationDataId = "COG_MARKER";
            ds.SetShape(new List<GeometryObject> { cylinder });

            ds.Name = "TRỌNG TÂM CẤU KIỆN";
        }
    }

  
    }