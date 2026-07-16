using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class CreatePPVCSectionCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            ViewPlan viewPlan = uidoc.ActiveView as ViewPlan;

            if (viewPlan == null)
            {
                TaskDialog.Show("Lỗi", "Vui lòng mở một mặt bằng (Plan View) để chạy lệnh này.");
                return Result.Failed;
            }

            try
            {
                // 1. Lấy ViewFamilyType phù hợp cho Elevation
                ViewFamilyType elevationViewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation);

                if (elevationViewFamilyType == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy ViewFamilyType phù hợp cho Elevation.");
                    return Result.Failed;
                }

                // 1b. Lấy ViewFamilyType phù hợp cho Section
                ViewFamilyType sectionViewFamilyType = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

                if (sectionViewFamilyType == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy ViewFamilyType phù hợp cho Section.");
                    return Result.Failed;
                }

                // 2. Chọn cấu kiện: ưu tiên selection có sẵn, nếu chưa chọn thì bắt quét chọn
                List<Element> selectedElements = new List<Element>();
                ICollection<ElementId> preSelected = uidoc.Selection.GetElementIds();
                if (preSelected != null && preSelected.Count > 0)
                {
                    foreach (ElementId id in preSelected)
                    {
                        Element el = doc.GetElement(id);
                        if (el != null)
                            selectedElements.Add(el);
                    }
                }
                else
                {
                    try
                    {
                        IList<Reference> refs = uidoc.Selection.PickObjects(
                            ObjectType.Element,
                            "Quét chọn các cấu kiện để tạo 4 hướng nhìn elevation"
                        );
                        foreach (Reference r in refs)
                        {
                            Element el = doc.GetElement(r);
                            if (el != null)
                                selectedElements.Add(el);
                        }
                    }
                    catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                    {
                        return Result.Cancelled;
                    }
                }

                if (selectedElements.Count == 0)
                {
                    return Result.Cancelled;
                }

                // 3. Tính toán BoundingBox bao phủ tất cả cấu kiện đã chọn
                BoundingBoxXYZ combinedBBox = null;
                foreach (Element el in selectedElements)
                {
                    BoundingBoxXYZ bbox = el.get_BoundingBox(null);
                    if (bbox == null) continue;

                    if (combinedBBox == null)
                    {
                        combinedBBox = new BoundingBoxXYZ
                        {
                            Min = bbox.Min,
                            Max = bbox.Max
                        };
                    }
                    else
                    {
                        combinedBBox.Min = new XYZ(
                            Math.Min(combinedBBox.Min.X, bbox.Min.X),
                            Math.Min(combinedBBox.Min.Y, bbox.Min.Y),
                            Math.Min(combinedBBox.Min.Z, bbox.Min.Z)
                        );
                        combinedBBox.Max = new XYZ(
                            Math.Max(combinedBBox.Max.X, bbox.Max.X),
                            Math.Max(combinedBBox.Max.Y, bbox.Max.Y),
                            Math.Max(combinedBBox.Max.Z, bbox.Max.Z)
                        );
                    }
                }

                if (combinedBBox == null)
                {
                    TaskDialog.Show("Lỗi", "Không tìm thấy cấu kiện có BoundingBox hợp lệ.");
                    return Result.Failed;
                }

                XYZ min = combinedBBox.Min;
                XYZ max = combinedBBox.Max;
                XYZ center = (min + max) / 2.0;

                // Tính toán khoảng cách offset thông minh (tối thiểu 3 feet hoặc 20% kích thước bao)
                double width = max.X - min.X;
                double depth = max.Y - min.Y;
                double size = Math.Max(width, depth);
                double offset = Math.Max(3.0, size * 0.2);

                // 4. Tạo 4 ElevationMarker, Elevation View và 4 Section View tương ứng
                using (Transaction tx = new Transaction(doc, "Tạo Elevation và Mặt cắt PPVC"))
                {
                    tx.Start();

                    // Hướng nhìn từ Tây sang Đông (Marker đặt ở phía Tây nhìn về phía Đông - Index 0)
                    XYZ posWest = new XYZ(max.X + offset, center.Y, center.Z);
                    ElevationMarker markerWest = ElevationMarker.CreateElevationMarker(doc, elevationViewFamilyType.Id, posWest, 50);
                    ViewSection viewEast = markerWest.CreateElevation(doc, viewPlan.Id, 0);
                    SetupView(doc, viewEast, combinedBBox, GetUniqueViewName(doc, "D"));

                    // Hướng nhìn từ Nam lên Bắc (Marker đặt ở phía Nam nhìn lên phía Bắc - Index 1)
                    XYZ posSouth = new XYZ(center.X, min.Y - offset, center.Z);
                    ElevationMarker markerSouth = ElevationMarker.CreateElevationMarker(doc, elevationViewFamilyType.Id, posSouth, 50);
                    ViewSection viewNorth = markerSouth.CreateElevation(doc, viewPlan.Id, 1);
                    SetupView(doc, viewNorth, combinedBBox, GetUniqueViewName(doc, "A"));

                    // Hướng nhìn từ Đông sang Tây (Marker đặt ở phía Đông nhìn về phía Tây - Index 2)
                    XYZ posEast = new XYZ(min.X - offset, center.Y, center.Z);
                    ElevationMarker markerEast = ElevationMarker.CreateElevationMarker(doc, elevationViewFamilyType.Id, posEast, 50);
                    ViewSection viewWest = markerEast.CreateElevation(doc, viewPlan.Id, 2);
                    SetupView(doc, viewWest, combinedBBox, GetUniqueViewName(doc, "B"));

                    // Hướng nhìn từ Bắc xuống Nam (Marker đặt ở phía Bắc nhìn xuống phía Nam - Index 3)
                    XYZ posNorth = new XYZ(center.X, max.Y + offset, center.Z);
                    ElevationMarker markerNorth = ElevationMarker.CreateElevationMarker(doc, elevationViewFamilyType.Id, posNorth, 50);
                    ViewSection viewSouth = markerNorth.CreateElevation(doc, viewPlan.Id, 3);
                    SetupView(doc, viewSouth, combinedBBox, GetUniqueViewName(doc, "C"));

                    // --- TẠO 4 MẶT CẮT THEO YÊU CẦU ---
                    // Tính toán các vị trí mặt cắt tương ứng 0.25 và 0.75 trên cả hai phương
                    double X1 = min.X + 0.25 * width;
                    double X2 = min.X + 0.75 * width;
                    double Y1 = min.Y + 0.25 * depth;
                    double Y2 = min.Y + 0.75 * depth;
                    double H = max.Z - min.Z;

                    double secBuffer = 0.5; // feet

                    // Mặt cắt 1: Cắt theo phương Y tại X1, nhìn về hướng Đông (+X)
                    CreateSection(doc, sectionViewFamilyType.Id,
                        new XYZ(X1, center.Y, center.Z),
                        new XYZ(0, 1, 0),
                        new XYZ(0, 0, 1),
                        new XYZ(1, 0, 0),
                        depth + 2 * secBuffer,
                        H + 2 * secBuffer,
                       1,
                        GetUniqueViewName(doc, "1")
                    );

                    // Mặt cắt 2: Cắt theo phương Y tại X2, nhìn về hướng Đông (+X)
                    CreateSection(doc, sectionViewFamilyType.Id,
                        new XYZ(X2, center.Y, center.Z),
                        new XYZ(0, 1, 0),
                        new XYZ(0, 0, 1),
                        new XYZ(-1, 0, 0),
                        depth + 2 * secBuffer,
                        H + 2 * secBuffer,
                      1,
                        GetUniqueViewName(doc, "2")
                    );

                    // Mặt cắt 3: Cắt theo phương X tại Y1, nhìn về hướng Bắc (+Y)
                    CreateSection(doc, sectionViewFamilyType.Id,
                        new XYZ(center.X, Y1, center.Z),
                        new XYZ(-1, 0, 0),
                        new XYZ(0, 0, 1),
                        new XYZ(0, 1, 0),
                        width + 2 * secBuffer,
                        H + 2 * secBuffer,
                       1,
                        GetUniqueViewName(doc, "3")
                    );

                    // Mặt cắt 4: Cắt theo phương X tại Y2, nhìn về hướng Bắc (+Y)
                    CreateSection(doc, sectionViewFamilyType.Id,
                        new XYZ(center.X, Y2, center.Z),
                        new XYZ(-1, 0, 0),
                        new XYZ(0, 0, 1),
                        new XYZ(0, -1, 0),
                        width + 2 * secBuffer,
                        H + 2 * secBuffer,
                      1,
                        GetUniqueViewName(doc, "4")
                    );

                    tx.Commit();
                }

                TaskDialog.Show("Thành công", "Đã tạo thành công 4 hướng nhìn elevation cho cấu kiện.");
                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Hệ Thống", ex.ToString());
                return Result.Failed;
            }
        }

        /// <summary>
        /// Thiết lập cấu hình crop box, chiều sâu hiển thị và kích hoạt chế độ crop cho view.
        /// </summary>
        private void SetupView(Document doc, ViewSection view, BoundingBoxXYZ combinedBBox, string name)
        {
            if (view == null) return;

            // Gán tên duy nhất
            view.Name = name;

            // Kích hoạt tính năng crop và hiển thị viền crop
            view.CropBoxActive = true;
            view.CropBoxVisible = true;

            // Lấy hệ trục tọa độ cục bộ của View (được xác định bởi CropBox.Transform)
            Transform transform = view.CropBox.Transform;
            Transform invTransform = transform.Inverse;

            // Lấy 8 đỉnh của BoundingBox tổng hợp
            XYZ min = combinedBBox.Min;
            XYZ max = combinedBBox.Max;

            XYZ[] corners = new XYZ[]
            {
                new XYZ(min.X, min.Y, min.Z),
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(min.X, max.Y, min.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z),
                new XYZ(max.X, min.Y, max.Z),
                new XYZ(min.X, max.Y, max.Z),
                new XYZ(max.X, max.Y, max.Z)
            };

            // Chuyển đổi toàn bộ đỉnh về tọa độ cục bộ của View
            List<XYZ> localCorners = corners.Select(pt => invTransform.OfPoint(pt)).ToList();

            // Tính toán giới hạn Min và Max theo hệ tọa độ cục bộ của View
            double minX = localCorners.Min(pt => pt.X);
            double maxX = localCorners.Max(pt => pt.X);
            double minY = localCorners.Min(pt => pt.Y);
            double maxY = localCorners.Max(pt => pt.Y);
            double minZ = localCorners.Min(pt => pt.Z);
            double maxZ = localCorners.Max(pt => pt.Z);

            // Bổ sung lề đệm (buffer = 0.5 feet ~ 150mm)
            double buffer = 0.5;
            minX -= buffer;
            maxX += buffer;
            minY -= buffer;
            maxY += buffer;
            minZ -= buffer;
            maxZ += buffer;

            // Cập nhật CropBox
            BoundingBoxXYZ cropBox = view.CropBox;
            cropBox.Min = new XYZ(minX, minY, minZ);
            cropBox.Max = new XYZ(maxX, maxY, maxZ);
            view.CropBox = cropBox;

            // Điều chỉnh Far Clip Offset bằng thuộc tính hệ thống
            // Trong hệ tọa độ cục bộ của Section View, camera nhìn theo trục -Z.
            // Do đó điểm xa nhất có giá trị Z âm sâu nhất. Khoảng cách cần nhìn là trị tuyệt đối của minZ.
            double depth = Math.Abs(minZ);
            Parameter farClipParam = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
            if (farClipParam != null && !farClipParam.IsReadOnly)
            {
                farClipParam.Set(depth);
            }
        }

        /// <summary>
        /// Tạo một mặt cắt (Section View) với các thông số vị trí, hướng nhìn, kích thước và đặt tên.
        /// </summary>
        private void CreateSection(Document doc, ElementId sectionTypeId, XYZ origin, XYZ basisX, XYZ basisY, XYZ basisZ, double width, double height, double depth, string name)
        {
            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = basisX;
            t.BasisY = basisY;
            t.BasisZ = basisZ;

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = t;
            sectionBox.Min = new XYZ(-width / 2.0, -height / 2.0, -0.1);
            sectionBox.Max = new XYZ(width / 2.0, height / 2.0, depth);

            ViewSection sectionView = ViewSection.CreateSection(doc, sectionTypeId, sectionBox);
            sectionView.Name = name;
            sectionView.CropBoxActive = false;
            sectionView.CropBoxVisible = false;
        }

        /// <summary>
        /// Tạo tên view không bị trùng lặp bằng cách đánh số thứ tự nếu trùng tên.
        /// </summary>
        private string GetUniqueViewName(Document doc, string baseName)
        {
            string name = baseName;
            int counter = 1;
            while (true)
            {
                bool exists = new FilteredElementCollector(doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                    return name;

                name = $"{baseName}_{counter}";
                counter++;
            }
        }
    }
}
