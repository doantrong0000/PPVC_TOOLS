using Autodesk.Revit.DB;
using System;
using System.Collections.Generic;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateDimModel
    {
        /// <summary>
        /// Tạo 2 đường Dim tổng cho toàn bộ Module theo phương X và phương Y dựa vào BoundingBox.
        /// </summary>
        /// <param name="view">View cần tạo dim.</param>
        /// <param name="elements">Các phần tử để lấy reference mặt phẳng xa nhất.</param>
        /// <param name="bbox">BoundingBox của toàn bộ module.</param>
        public static void CreateOverallDimensions(View view, List<Element> elements, BoundingBoxXYZ bbox)
        {
            if (view == null || elements == null || elements.Count == 0 || bbox == null) return;
            Document doc = view.Document;

            double offsetOuter = 3.2; // Khoảng cách từ biên tường đến đường dim tổng (feet)
            double buffer = 1.0;      // Độ vươn biên của đường thẳng dim (feet)

            double minX = bbox.Min.X;
            double maxX = bbox.Max.X;
            double minY = bbox.Min.Y;
            double maxY = bbox.Max.Y;

            // 1. DIM TỔNG THEO PHƯƠNG Y (CẠNH TRÁI)
            Reference southRef = GetFurthestFace(elements, view, XYZ.BasisY, minY, false);
            Reference northRef = GetFurthestFace(elements, view, XYZ.BasisY, maxY, false);
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

            // 2. DIM TỔNG THEO PHƯƠNG X (CẠNH TRÊN)
            Reference westRef = GetFurthestFace(elements, view, XYZ.BasisX, minX, true);
            Reference eastRef = GetFurthestFace(elements, view, XYZ.BasisX, maxX, true);

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

        /// <summary>
        /// Tìm mặt phẳng ngoài cùng của danh sách cấu kiện theo phương và tọa độ mục tiêu.
        /// </summary>
        private static Reference GetFurthestFace(List<Element> elements, View view, XYZ normalDirection, double targetValue, bool checkX)
        {
            Reference bestRef = null;
            double minDiff = double.MaxValue;

            Options opt = new Options
            {
                ComputeReferences = true,
                View = view               // Lấy hình học cắt qua View hiện tại (đáp ứng đúng Revit API khi view được chỉ định)
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
                            // Kiểm tra pháp tuyến có song song/trùng với hướng chỉ định không
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


    }
}
