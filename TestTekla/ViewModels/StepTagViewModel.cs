// ViewModels\StepTagViewModel.cs
using System;
using System.Collections.Generic;
using Tekla.Structures.Drawing;
using Tekla.Structures.Drawing.UI;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using Line = Tekla.Structures.Drawing.Line;

namespace TeklaApp.ViewModels
{
    public class StepTagViewModel
    {
        /// <summary>
        /// Tạo ký hiệu giật cấp tại vị trí giao nhau giữa các cấu kiện.
        /// Logic đã được tách biệt cho phương dọc và phương ngang để đảm bảo hiển thị đúng thị giác.
        /// </summary>
        public string CreateStepTag(double textHeight, string fontName, double surfLen, double stepHeight, double hatchSpc, double hatchLen, bool useRectFill = false, string fillName = "ANSI31_13")
        {
            DrawingHandler dh = new DrawingHandler();
            if (dh.GetActiveDrawing() == null)
            {
                return "Error: Please open a drawing before running this command.";
            }

            try
            {
                var selector = dh.GetDrawingObjectSelector();
                var dObjectsEnum = selector.GetSelected();
                Tekla.Structures.Model.Model model = new Tekla.Structures.Model.Model();

                var dParts = new List<Tekla.Structures.Drawing.Part>();
                foreach (var dObj in dObjectsEnum)
                {
                    if (dObj is Tekla.Structures.Drawing.Part dp)
                    {
                        dParts.Add(dp);
                    }
                }

                if (dParts.Count < 2)
                {
                    return "Error: Please select at least two parts (Beams or Slabs).";
                }

                int tagCreatedCount = 0;

                for (int i = 0; i < dParts.Count; i++)
                {
                    for (int j = i + 1; j < dParts.Count; j++)
                    {
                        var dp1 = dParts[i];
                        var dp2 = dParts[j];

                        var mPart1 = model.SelectModelObject(dp1.ModelIdentifier) as Tekla.Structures.Model.Part;
                        var mPart2 = model.SelectModelObject(dp2.ModelIdentifier) as Tekla.Structures.Model.Part;

                        if (mPart1 == null || mPart2 == null) continue;

                        Solid solid1 = mPart1.GetSolid();
                        Solid solid2 = mPart2.GetSolid();

                        double z1 = solid1.MaximumPoint.Z;
                        double z2 = solid2.MaximumPoint.Z;

                        if (Math.Abs(z1 - z2) < 0.1) continue;

                        ViewBase view = dp1.GetView();
                        double scale = 1.0;
                        Tekla.Structures.Drawing.View realView = view as Tekla.Structures.Drawing.View;
                        Matrix toViewMatrix = null;

                        if (realView != null)
                        {
                            scale = realView.Attributes.Scale;
                            CoordinateSystem sys = realView.DisplayCoordinateSystem;
                            toViewMatrix = MatrixFactory.ToCoordinateSystem(sys);
                        }

                        Point s1Min = toViewMatrix != null ? toViewMatrix.Transform(solid1.MinimumPoint) : solid1.MinimumPoint;
                        Point s1Max = toViewMatrix != null ? toViewMatrix.Transform(solid1.MaximumPoint) : solid1.MaximumPoint;
                        Point s2Min = toViewMatrix != null ? toViewMatrix.Transform(solid2.MinimumPoint) : solid2.MinimumPoint;
                        Point s2Max = toViewMatrix != null ? toViewMatrix.Transform(solid2.MaximumPoint) : solid2.MaximumPoint;

                        double v1MinX = Math.Min(s1Min.X, s1Max.X); double v1MaxX = Math.Max(s1Min.X, s1Max.X);
                        double v1MinY = Math.Min(s1Min.Y, s1Max.Y); double v1MaxY = Math.Max(s1Min.Y, s1Max.Y);
                        double v2MinX = Math.Min(s2Min.X, s2Max.X); double v2MaxX = Math.Max(s2Min.X, s2Max.X);
                        double v2MinY = Math.Min(s2Min.Y, s2Max.Y); double v2MaxY = Math.Max(s2Min.Y, s2Max.Y);

                        double overMinX = Math.Max(v1MinX, v2MinX);
                        double overMaxX = Math.Min(v1MaxX, v2MaxX);
                        double overMinY = Math.Max(v1MinY, v2MinY);
                        double overMaxY = Math.Min(v1MaxY, v2MaxY);

                        if (overMinX > overMaxX + 1.0 || overMinY > overMaxY + 1.0) continue;

                        double overLenX = overMaxX - overMinX;
                        double overLenY = overMaxY - overMinY;

                        Point pJ = new Point((overMinX + overMaxX) / 2.0, (overMinY + overMaxY) / 2.0, 0);

                        // --- TÁCH BIỆT LOGIC PHƯƠNG DỌC VÀ NGANG ---
                        Vector vAlong;
                        Vector vHigh, vLow;
                        bool isJointHorizontal = overLenX >= overLenY;
                        bool isPart1High = z1 > z2;
                        Point center1_view = new Point((v1MinX + v1MaxX) / 2.0, (v1MinY + v1MaxY) / 2.0, 0);
                        Vector vecToC1 = new Vector(center1_view.X - pJ.X, center1_view.Y - pJ.Y, 0);

                        if (isJointHorizontal)
                        {
                            // 1. Mối nối ngang: Ưu tiên đọc từ Trái sang Phải
                            vAlong = new Vector(1, 0, 0);
                            Vector vPerpLocal = new Vector(0, 1, 0); // Phương đứng
                            double dot = vecToC1.Dot(vPerpLocal);

                            vHigh = isPart1High ? (dot > 0 ? vPerpLocal : Neg(vPerpLocal)) : (dot > 0 ? Neg(vPerpLocal) : vPerpLocal);
                            vLow = Neg(vHigh);

                            // Nếu sàn Cao ở phía dưới (Y < 0), đảo vAlong để chữ Z không bị ngược thị giác
                            if (vHigh.Y > 0) vAlong = Neg(vAlong);
                        }
                        else
                        {
                            // 2. Mối nối dọc: Ưu tiên đọc từ Dưới lên Trên
                            vAlong = new Vector(0, 1, 0);
                            Vector vPerpLocal = new Vector(1, 0, 0); // Phương ngang
                            double dot = vecToC1.Dot(vPerpLocal);

                            vHigh = isPart1High ? (dot > 0 ? vPerpLocal : Neg(vPerpLocal)) : (dot > 0 ? Neg(vPerpLocal) : vPerpLocal);
                            vLow = Neg(vHigh);

                            // NÂNG CẤP QUAN TRỌNG: Nếu sàn cao ở bên PHẢI (X > 0), 
                            // đảo vAlong xuống dưới để pLowJ nằm thấp hơn pJ -> tạo hình bậc thang đi lên.
                            if (vHigh.X > 0) vAlong = Neg(vAlong);
                        }

                        double sSurf = surfLen * scale;
                        double sStep = stepHeight * scale;

                        // ======== Z-bar geometry ========
                        Point pHighEnd = new Point(pJ.X + vHigh.X * sSurf, pJ.Y + vHigh.Y * sSurf, 0);
                        Point pLowJ = new Point(pJ.X + vAlong.X * sStep, pJ.Y + vAlong.Y * sStep, 0);
                        Point pLowEnd = new Point(pLowJ.X + vLow.X * sSurf, pLowJ.Y + vLow.Y * sSurf, 0);

                      
                        // ======== Hatching ========
                        if (!useRectFill)
                        {
                            Vector hatchDir = isJointHorizontal ? new Vector(0.707, 0.707, 0) : new Vector(0.707, -0.707, 0);
                            DrawHatchAlongLine(view, pHighEnd, pJ, hatchDir, hatchSpc * scale, hatchLen * scale);
                            DrawHatchAlongLine(view, pLowJ, pLowEnd, hatchDir, hatchSpc * scale, hatchLen * scale);
                        }
                        else
                        {
                            double sHatchLen = hatchLen * scale;

                            var hPolyPts = new PointList();
                            hPolyPts.Add(new Point(pHighEnd.X, pHighEnd.Y, pHighEnd.Z));
                            hPolyPts.Add(new Point(pJ.X, pJ.Y, pJ.Z));
                            hPolyPts.Add(new Point(pJ.X + vAlong.X * sHatchLen, pJ.Y + vAlong.Y * sHatchLen, pJ.Z));
                            hPolyPts.Add(new Point(pHighEnd.X + vAlong.X * sHatchLen, pHighEnd.Y + vAlong.Y * sHatchLen, pHighEnd.Z));
                            hPolyPts.Add(new Point(pHighEnd.X, pHighEnd.Y, pHighEnd.Z));

                            var hPoly = new Tekla.Structures.Drawing.Polygon(view, hPolyPts);
                            hPoly.Attributes.Hatch.Name = fillName;
                            hPoly.Attributes.Hatch.Color = DrawingHatchColors.Black;
                            hPoly.Attributes.Line.Color = DrawingColors.Invisible;
                            hPoly.Insert();

                            var lPolyPts = new PointList();
                            lPolyPts.Add(new Point(pLowJ.X, pLowJ.Y, pLowJ.Z));
                            lPolyPts.Add(new Point(pLowEnd.X, pLowEnd.Y, pLowEnd.Z));
                            lPolyPts.Add(new Point(pLowEnd.X + vAlong.X * sHatchLen, pLowEnd.Y + vAlong.Y * sHatchLen, pLowEnd.Z));
                            lPolyPts.Add(new Point(pLowJ.X + vAlong.X * sHatchLen, pLowJ.Y + vAlong.Y * sHatchLen, pLowJ.Z));
                            lPolyPts.Add(new Point(pLowJ.X, pLowJ.Y, pLowJ.Z));

                            var lPoly = new Tekla.Structures.Drawing.Polygon(view, lPolyPts);
                            lPoly.Attributes.Hatch.Name = fillName;
                            lPoly.Attributes.Hatch.Color = DrawingHatchColors.Black;
                            lPoly.Attributes.Line.Color = DrawingColors.Invisible;
                            lPoly.Insert();
                        }


                        new Line(view, pHighEnd, pJ).Insert();
                        new Line(view, pJ, pLowJ).Insert();
                        new Line(view, pLowJ, pLowEnd).Insert();

                        // ======== Text ========
                        Point textPos = new Point(
                            (pJ.X + pLowJ.X) / 2.0 + vLow.X * sSurf * 0.3,
                            (pJ.Y + pLowJ.Y) / 2.0 + vLow.Y * sSurf * 0.3, 0);

                        Text text = new Text(view, textPos, ((int)Math.Round(Math.Abs(z1 - z2))).ToString());
                        text.Attributes = new Text.TextAttributes();
                        text.Placing = new PointPlacing();
                        text.Attributes.Font.Height = textHeight;
                        text.Attributes.Font.Name = fontName;
                        text.Attributes.Font.Color = DrawingColors.Green;

                        Vector vPerp = new Vector(vAlong.Y, -vAlong.X, 0);
                        double angleDeg = Math.Atan2(vPerp.Y, vPerp.X) * 180.0 / Math.PI;
                        if (angleDeg > 90) angleDeg -= 180;
                        if (angleDeg < -90) angleDeg += 180;
                        text.Attributes.Angle = angleDeg;
                        text.Insert();

                        tagCreatedCount++;
                    }
                }

                if (tagCreatedCount > 0)
                {
                    dh.GetActiveDrawing().CommitChanges();
                    return $"Success: Created {tagCreatedCount} step tags.";
                }
                return "No valid step tags created.";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        private void DrawHatchAlongLine(ViewBase view, Point from, Point to, Vector hatchDir, double spacing, double len)
        {
            Vector dir = new Vector(to.X - from.X, to.Y - from.Y, 0);
            double lineLen = dir.GetLength();

            // Nếu đường line quá ngắn hoặc khoảng cách hatch quá nhỏ thì không vẽ
            if (lineLen < 0.1 || spacing < 0.1) return;

            Vector dirN = new Vector(dir.X, dir.Y, 0);
            dirN.Normalize();

            // --- CẢI TIẾN: Thêm khoảng đệm (Padding) ---
            // Chúng ta bắt đầu từ 0.5 * spacing và kết thúc trước khi chạm điểm 'to'
            double startPadding = spacing * 0.5;
            double endPadding = spacing * 0.5;
            double effectiveLen = lineLen - (startPadding + endPadding);

            if (effectiveLen <= 0) return; // Nếu đường line ngắn hơn tổng khoảng đệm

            int n = (int)(effectiveLen / spacing);

            for (int h = 0; h <= n; h++)
            {
                // t bắt đầu từ startPadding thay vì từ 0
                double t = startPadding + (h * spacing);

                Point hs = new Point(from.X + dirN.X * t, from.Y + dirN.Y * t, 0);
                Point he = new Point(hs.X + hatchDir.X * len, hs.Y + hatchDir.Y * len, 0);

                var l = new Line(view, hs, he);
                l.Attributes.Line.Color = DrawingColors.Black;
                l.Insert();
            }
        }

        // Helper to negate a Vector (Vector doesn't define unary - operator)
        private Vector Neg(Vector v) => new Vector(-v.X, -v.Y, -v.Z);
    }
}