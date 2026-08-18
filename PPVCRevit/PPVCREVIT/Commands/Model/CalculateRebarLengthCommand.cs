using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

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

            List<Rebar> rebars = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Rebar)
                .WhereElementIsNotElementType()
                .Cast<Rebar>()
                .ToList();

            if (rebars.Count == 0)
            {
                TaskDialog.Show("Thông báo", "Không tìm thấy thanh thép nào trong dự án.");
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
                        barDiameter = barType.BarNominalDiameter;
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

            // 1. Góc uốn <= 135 độ: Tính theo đỉnh giao điểm 2 tiếp tuyến
            if (centralAngle <= (3.0 * Math.PI / 4.0) + 1e-4)
            {
                return 2.0 * rOut * Math.Tan(centralAngle / 2.0);
            }
            // 2. Góc uốn > 135 độ (Bao gồm móc 180 độ, móc 270 độ, vòng lặp 360 độ)
            // Khoảng cách phủ bì bao ngoài lớn nhất chính là đường kính uốn ngoài (2 * R_out)
            else
            {
                return 2.0 * rOut;
            }
        }
    }
}