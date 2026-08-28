using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Utils.Filters;

namespace PPVCREVIT.Commands.Model
{
    // To create the length of bar same in the Tekla
    [Transaction(TransactionMode.Manual)]
    public class CalculateRebarLengthCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            List<Rebar> rebars = new List<Rebar>();

            // 1. Kiểm tra đối tượng đang chọn trước khi gọi lệnh
            ICollection<ElementId> currentSelectedIds = uidoc.Selection.GetElementIds();
            if (currentSelectedIds != null && currentSelectedIds.Count > 0)
            {
                foreach (ElementId id in currentSelectedIds)
                {
                    Element elem = doc.GetElement(id);
                    if (elem is Rebar rebar)
                    {
                        rebars.Add(rebar);
                    }
                }
            }

            // 2. Nếu chưa chọn thanh thép nào, yêu cầu người dùng quét chọn trên màn hình
            if (rebars.Count == 0)
            {
                try
                {
                    IList<Element> pickedElements = uidoc.Selection.PickElementsByRectangle(
                        new RebarFilter.RebarSelectionFilter(),
                        "Quét chọn các thanh thép cần tính chiều dài");

                    foreach (Element elem in pickedElements)
                    {
                        if (elem is Rebar rebar)
                        {
                            rebars.Add(rebar);
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }
            }

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không có thanh thép nào được chọn.");
                return Result.Cancelled;
            }

            using (Transaction tx = new Transaction(doc, "Calculate Rebar Out-to-Out Length"))
            {
                tx.Start();

                foreach (Rebar rebar in rebars)
                {
                    // Lấy số hiệu thanh thép (Rebar Number / Schedule Mark)
                    string rebarNumber = "";
                    Parameter numParam = rebar.get_Parameter(BuiltInParameter.REBAR_NUMBER);
                    if (numParam != null && numParam.HasValue)
                    {
                        rebarNumber = numParam.AsString();
                    }
                    else
                    {
                        // Fallback sang Schedule Mark nếu dự án dùng partition riêng
                        Parameter schedMarkParam = rebar.LookupParameter("Schedule Mark");
                        if (schedMarkParam != null && schedMarkParam.HasValue)
                        {
                            rebarNumber = schedMarkParam.AsString();
                        }
                    }

                    // ==============================================================
                    // ĐOẠN ĐẶT ĐIỂM DỪNG DEBUG TẠI THANH CÓ SỐ HIỆU LÀ "1"
                    // ==============================================================
                    if (rebarNumber == "1" || rebar.Id.Value == 1) // Kiểm tra theo Rebar Number hoặc Id
                    {
                        int debugHere = 1;
                    }

                    // Lấy đường kính danh định của thanh thép (Bar Diameter: d)
                    double barDiameter = 0;
                    RebarBarType barType = doc.GetElement(rebar.GetTypeId()) as RebarBarType;
                    if (barType != null)
                    {
                        barDiameter = barType.BarModelDiameter;
                    }

                    double totalOutToOutLength = 0;

                    // suppressBendRadius = false để giữ các cung uốn Arc
                    // suppressHooks = false để giữ lại các đoạn móc
                    IList<Curve> curves = rebar.GetCenterlineCurves(false, false, false, MultiplanarOption.IncludeAllMultiplanarCurves, 0);

                    if (curves != null && curves.Count > 0)
                    {
                        foreach (Curve curve in curves)
                        {
                            if (curve is Arc arc)
                            {
                                totalOutToOutLength += CalculateArcOutToOutLength(arc, barDiameter);
                            }
                            else
                            {
                                totalOutToOutLength += curve.Length;
                            }
                        }
                    }

                    if (totalOutToOutLength <= 0)
                    {
                        Parameter builtInLength = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                        if (builtInLength != null && builtInLength.HasValue)
                        {
                            totalOutToOutLength = builtInLength.AsDouble();
                        }
                    }

                    // Gán kết quả vào Shared Parameter
                    Parameter targetParam = rebar.LookupParameter("WH_Rebar_Dimension_BarLength");
                    if (targetParam != null && !targetParam.IsReadOnly)
                    {
                        targetParam.Set(totalOutToOutLength);
                    }
                }

                tx.Commit();
            }

            TaskDialog.Show("Hoàn tất", $"Đã tính toán chiều dài cho {rebars.Count} thanh thép.");
            return Result.Succeeded;
        }

        /// <summary>
        /// Tính chiều dài đoạn uốn quy đổi mép ngoài, chặn góc lớn > 135 độ và góc xoay 270 độ
        /// </summary>
        private double CalculateArcOutToOutLength(Arc arc, double barDiameter)
        {
            double rCenter = arc.Radius;
            double rOut = rCenter + (barDiameter / 2.0); // Bán kính mép ngoài
            double arcLength = arc.Length;
            double centralAngle = arcLength / rCenter;    // Góc ở tâm (radian)

            // 1. Góc uốn chuẩn hình học (<= 135 độ hay 3*PI/4 rad): Tính giao điểm 2 tiếp tuyến mép ngoài
            if (centralAngle <= (3.0 * Math.PI / 4.0) + 1e-4)
            {
                return 2.0 * rOut * Math.Tan(centralAngle / 2.0);
            }
            // 2. Góc xoay móc Hook 180 độ (Bán nguyệt: từ 135 độ đến 180 độ)
            else if (centralAngle <= Math.PI + 1e-4)
            {
                return 2.0 * rOut;
            }
            // 3. Góc xoay uốn Hook 270 độ (3*PI/2 rad) hoặc vòng lặp:
            else
            {
                return 2.0 * rOut * Math.Tan(centralAngle / 2.0);
            }
        }
    }


}