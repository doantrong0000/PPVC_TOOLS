// ViewModels\StepTagViewModel.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;
using Tekla.Structures.Drawing.UI;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Solid;
using Line = Tekla.Structures.Drawing.Line;

namespace TeklaApp.ViewModels
{
    public class StepTagViewModel
    {
        // ── Inner type: groups top faces sharing the same Z level ──
        private class TopFaceGroup
        {
            public double ZLevel;
            public List<Point> ViewVertices = new List<Point>();

            public double MinX, MaxX, MinY, MaxY;

            public void ComputeBounds()
            {
                MinX = double.MaxValue; MaxX = double.MinValue;
                MinY = double.MaxValue; MaxY = double.MinValue;
                foreach (var pt in ViewVertices)
                {
                    if (pt.X < MinX) MinX = pt.X;
                    if (pt.X > MaxX) MaxX = pt.X;
                    if (pt.Y < MinY) MinY = pt.Y;
                    if (pt.Y > MaxY) MaxY = pt.Y;
                }
            }

            public Point Centroid()
            {
                double cx = 0, cy = 0;
                foreach (var pt in ViewVertices) { cx += pt.X; cy += pt.Y; }
                int n = ViewVertices.Count;
                return n > 0 ? new Point(cx / n, cy / n, 0) : new Point(0, 0, 0);
            }
        }

        /// <summary>
        /// Tạo ký hiệu giật cấp.
        /// Trường hợp 1: chọn ≥ 2 parts → so sánh Z giữa từng cặp (logic cũ).
        /// Trường hợp 2: mỗi part được chọn cũng được quét face để tìm giật cấp
        ///               bên trong chính nó (sàn bị cắt / Boolean cut).
        /// </summary>
        public string CreateStepTag(
            double textHeight, string fontName, string textColor,
            double surfLen, double stepHeight, double hatchLen,
            string fillName = "ANSI32_A", double scaleX = 0.05, double scaleY = 0.05)

        {
            DrawingHandler dh = new DrawingHandler();
            if (dh.GetActiveDrawing() == null)
                return "Error: Please open a drawing before running this command.";

            try
            {
                var selector = dh.GetDrawingObjectSelector();
                var dObjectsEnum = selector.GetSelected();
                Tekla.Structures.Model.Model model = new Tekla.Structures.Model.Model();

                var dParts = new List<Tekla.Structures.Drawing.Part>();
                foreach (var dObj in dObjectsEnum)
                {
                    if (dObj is Tekla.Structures.Drawing.Part dp)
                        dParts.Add(dp);
                }

                if (dParts.Count < 1)
                    return "Error: Please select at least one part.";

                int tagCreatedCount = 0;

                // ════════════════════════════════════════════════════
                // A) Pair analysis – logic cũ (2 parts khác nhau)
                // ════════════════════════════════════════════════════
                for (int i = 0; i < dParts.Count; i++)
                {
                    for (int j = i + 1; j < dParts.Count; j++)
                    {
                        var dp1 = dParts[i];
                        var dp2 = dParts[j];

                        var mPart1 = model.SelectModelObject(dp1.ModelIdentifier) as Tekla.Structures.Model.Part;
                        var mPart2 = model.SelectModelObject(dp2.ModelIdentifier) as Tekla.Structures.Model.Part;
                        if (mPart1 == null || mPart2 == null) continue;

                        Tekla.Structures.Model.Solid solid1 = mPart1.GetSolid();
                        Tekla.Structures.Model.Solid solid2 = mPart2.GetSolid();

                        double z1 = solid1.MaximumPoint.Z;
                        double z2 = solid2.MaximumPoint.Z;
                        if (Math.Abs(z1 - z2) < 0.1) continue;

                        ViewBase view = dp1.GetView();
                        double scale = 1.0;
                        Matrix toViewMatrix = null;
                        var realView = view as Tekla.Structures.Drawing.View;
                        if (realView != null)
                        {
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
                        if (overLenX + overLenY < 10) continue;

                        Point pJ = new Point((overMinX + overMaxX) / 2.0, (overMinY + overMaxY) / 2.0, 0);

                        Vector vAlong;
                        Vector vHigh, vLow;
                        bool isJointHorizontal = overLenX >= overLenY;
                        bool isPart1High = z1 > z2;
                        Point center1_view = new Point((v1MinX + v1MaxX) / 2.0, (v1MinY + v1MaxY) / 2.0, 0);

                        if (isJointHorizontal)
                        {
                            vAlong = new Vector(1, 0, 0);
                            Vector vUp = new Vector(0, 1, 0);
                            bool isPart1Above = center1_view.Y > pJ.Y;
                            vHigh = isPart1High ? (isPart1Above ? vUp : Neg(vUp)) : (isPart1Above ? Neg(vUp) : vUp);
                            vLow = Neg(vHigh);
                        }
                        else
                        {
                            vAlong = new Vector(0, -1, 0);
                            Vector vRight = new Vector(1, 0, 0);
                            bool isPart1OnLeft = center1_view.X < pJ.X;
                            vHigh = isPart1High ? (isPart1OnLeft ? Neg(vRight) : vRight) : (isPart1OnLeft ? vRight : Neg(vRight));
                            vLow = Neg(vHigh);
                        }

                        DrawStepSymbol(view, scale, pJ, vAlong, vHigh, vLow,
                            Math.Abs(z1 - z2), surfLen, stepHeight, hatchLen,
                            textHeight, fontName, textColor, fillName, scaleX, scaleY);
                        tagCreatedCount++;
                    }
                }

                // ════════════════════════════════════════════════════
                // B) Single-part analysis – tìm giật cấp trong 1 part
                //    (sàn bị cắt / BooleanPart tạo bậc)
                // ════════════════════════════════════════════════════
                foreach (var dp in dParts)
                {
                    tagCreatedCount += ProcessSinglePartSteps(
                        dp, model, surfLen, stepHeight, hatchLen,
                        textHeight, fontName, textColor, fillName, scaleX, scaleY);
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

        // ──────────────────────────────────────────────────────
        //  Single-part step detection: quét top faces, nhóm Z
        // ──────────────────────────────────────────────────────
        private int ProcessSinglePartSteps(
            Tekla.Structures.Drawing.Part dp,
            Tekla.Structures.Model.Model model,
            double surfLen, double stepHeight, double hatchLen,
            double textHeight, string fontName, string textColor,
            string fillName, double scaleX, double scaleY)
        {
            var mPart = model.SelectModelObject(dp.ModelIdentifier) as Tekla.Structures.Model.Part;
            if (mPart == null) return 0;

            Tekla.Structures.Model.Solid solid = mPart.GetSolid();
            if (solid == null) return 0;

            ViewBase view = dp.GetView();
            double scale = 1.0;
            Matrix toViewMatrix = null;

            var realView = view as Tekla.Structures.Drawing.View;
            if (realView != null)
            {
                CoordinateSystem sys = realView.DisplayCoordinateSystem;
                toViewMatrix = MatrixFactory.ToCoordinateSystem(sys);
            }

            // Tìm các nhóm top-face theo mức Z
            var groups = FindTopFaceGroups(solid, toViewMatrix);
            if (groups.Count < 2) return 0; // không có bậc

            int count = 0;

            // Duyệt từng cặp liền kề (đã sort Z giảm dần)
            for (int k = 0; k < groups.Count - 1; k++)
            {
                var highGroup = groups[k];
                var lowGroup = groups[k + 1];
                double deltaZ = highGroup.ZLevel - lowGroup.ZLevel;
                if (deltaZ < 0.1) continue;

                Point cHigh = highGroup.Centroid();
                Point cLow = lowGroup.Centroid();

                double dx = cLow.X - cHigh.X;
                double dy = cLow.Y - cHigh.Y;
                double dist = Math.Sqrt(dx * dx + dy * dy);
                if (dist < 0.1) continue;

                // ── Xác định hướng joint và vị trí ranh giới ──
                Vector vAlong, vHigh, vLow;
                Point pJ;

                if (Math.Abs(dy) >= Math.Abs(dx))
                {
                    // Hai nhóm xếp theo phương Y → ranh giới ngang (horizontal)
                    vAlong = new Vector(1, 0, 0);
                    vHigh = (cHigh.Y > cLow.Y) ? new Vector(0, 1, 0) : new Vector(0, -1, 0);
                    vLow = Neg(vHigh);

                    // Tìm toạ độ Y ranh giới
                    double boundaryY;
                    if (cHigh.Y > cLow.Y)
                        boundaryY = (highGroup.MinY + lowGroup.MaxY) / 2.0;
                    else
                        boundaryY = (highGroup.MaxY + lowGroup.MinY) / 2.0;

                    double overlapMinX = Math.Max(highGroup.MinX, lowGroup.MinX);
                    double overlapMaxX = Math.Min(highGroup.MaxX, lowGroup.MaxX);
                    double boundaryX = (overlapMinX + overlapMaxX) / 2.0;

                    pJ = new Point(boundaryX, boundaryY, 0);
                }
                else
                {
                    // Hai nhóm xếp theo phương X → ranh giới dọc (vertical)
                    vAlong = new Vector(0, -1, 0);
                    vHigh = (cHigh.X > cLow.X) ? new Vector(1, 0, 0) : new Vector(-1, 0, 0);
                    vLow = Neg(vHigh);

                    double boundaryX;
                    if (cHigh.X > cLow.X)
                        boundaryX = (highGroup.MinX + lowGroup.MaxX) / 2.0;
                    else
                        boundaryX = (highGroup.MaxX + lowGroup.MinX) / 2.0;

                    double overlapMinY = Math.Max(highGroup.MinY, lowGroup.MinY);
                    double overlapMaxY = Math.Min(highGroup.MaxY, lowGroup.MaxY);
                    double boundaryY = (overlapMinY + overlapMaxY) / 2.0;

                    pJ = new Point(boundaryX, boundaryY, 0);
                }

                DrawStepSymbol(view, scale, pJ, vAlong, vHigh, vLow,
                    deltaZ, surfLen, stepHeight, hatchLen,
                    textHeight, fontName, textColor, fillName, scaleX, scaleY);
                count++;
            }

            return count;
        }

        // ──────────────────────────────────────────────────────
        //  Quét Solid faces → nhóm top faces theo mức Z
        // ──────────────────────────────────────────────────────
        private List<TopFaceGroup> FindTopFaceGroups(Tekla.Structures.Model.Solid solid, Matrix toViewMatrix)
        {
            var faceEnum = solid.GetFaceEnumerator();
            var faceDatas = new List<(double zLevel, List<Point> viewVerts)>();

            while (faceEnum.MoveNext())
            {
                var face = faceEnum.Current as Face;
                if (face == null) continue;

                Vector normal = face.Normal;
                // Chỉ lấy mặt hướng lên (top faces)
                if (normal.Z < 0.7) continue;

                // Thu thập vertices qua Loop → Vertex
                var vertices3D = new List<Point>();
                var loopEnum = face.GetLoopEnumerator();
                while (loopEnum.MoveNext())
                {
                    var loop = loopEnum.Current as Loop;
                    if (loop == null) continue;
                    var vertEnum = loop.GetVertexEnumerator();
                    while (vertEnum.MoveNext())
                    {
                        var vertex = vertEnum.Current as Point;
                        if (vertex != null)
                            vertices3D.Add(vertex);
                    }
                }

                if (vertices3D.Count == 0) continue;

                // Tính Z trung bình (toạ độ model)
                double avgZ = 0;
                foreach (var v in vertices3D) avgZ += v.Z;
                avgZ /= vertices3D.Count;

                // Chuyển sang toạ độ view 2D
                var viewVerts = new List<Point>();
                foreach (var v in vertices3D)
                    viewVerts.Add(toViewMatrix != null ? toViewMatrix.Transform(v) : v);

                faceDatas.Add((avgZ, viewVerts));
            }

            // ── Nhóm theo mức Z (tolerance 1 mm) ──
            var groups = new List<TopFaceGroup>();
            foreach (var (zLevel, viewVerts) in faceDatas)
            {
                TopFaceGroup match = null;
                foreach (var g in groups)
                {
                    if (Math.Abs(g.ZLevel - zLevel) < 1.0)
                    {
                        match = g;
                        break;
                    }
                }
                if (match == null)
                {
                    match = new TopFaceGroup { ZLevel = zLevel };
                    groups.Add(match);
                }
                match.ViewVertices.AddRange(viewVerts);
            }

            foreach (var g in groups) g.ComputeBounds();

            // Sort Z giảm dần (cao → thấp)
            groups.Sort((a, b) => b.ZLevel.CompareTo(a.ZLevel));

            return groups;
        }

        // ──────────────────────────────────────────────────────
        //  Vẽ ký hiệu giật cấp (Z-bar + Hatch + Text)
        // ──────────────────────────────────────────────────────
        private void DrawStepSymbol(
            ViewBase view, double scale,
            Point pJ, Vector vAlong, Vector vHigh, Vector vLow,
            double deltaZ,
            double surfLen, double stepHeight, double hatchLen,
            double textHeight, string fontName, string textColor,
            string fillName, double scaleX, double scaleY)
        {
            double sSurf = surfLen * scale;
            double sStep = stepHeight * scale;

            // ======== Z-bar geometry ========
            Point pHighEnd = new Point(pJ.X + vHigh.X * sSurf, pJ.Y + vHigh.Y * sSurf, 0);
            Point pLowJ = new Point(pJ.X + vAlong.X * sStep, pJ.Y + vAlong.Y * sStep, 0);
            Point pLowEnd = new Point(pLowJ.X + vLow.X * sSurf, pLowJ.Y + vLow.Y * sSurf, 0);

            // ======== Hatching / Fill ========
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
            hPoly.Attributes.Hatch.ScaleX = scaleX;
            hPoly.Attributes.Hatch.ScaleY = scaleY;
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
            lPoly.Attributes.Hatch.ScaleX = scaleX;
            lPoly.Attributes.Hatch.ScaleY = scaleY;
            lPoly.Attributes.Line.Color = DrawingColors.Invisible;
            lPoly.Insert();

            new Line(view, pHighEnd, pJ).Insert();
            new Line(view, pJ, pLowJ).Insert();
            new Line(view, pLowJ, pLowEnd).Insert();

            // ======== Text ========
            Point textPos = new Point(
                pJ.X + vLow.X * surfLen * 0.5,
                pJ.Y + vLow.Y * surfLen * 0.5, 0);

            Text text = new Text(view, textPos, ((int)Math.Round(deltaZ)).ToString());
            text.Attributes = new Text.TextAttributes();
            text.Placing = new PointPlacing();
            text.Attributes.Frame = new Frame(FrameTypes.None, DrawingColors.Black);
            text.Attributes.Font.Height = textHeight;
            text.Attributes.Font.Name = fontName;
            text.Attributes.Font.Color = GetDrawingColor(textColor);

            Vector vPerp = new Vector(vAlong.Y, -vAlong.X, 0);
            double angleDeg = Math.Atan2(vPerp.Y, vPerp.X) * 180.0 / Math.PI;
            if (angleDeg > 90) angleDeg -= 180;
            if (angleDeg < -90) angleDeg += 180;
            text.Attributes.Angle = angleDeg;
            text.Insert();
        }

        // ──────────────────────────────────────────────────────
        //  Helpers
        // ──────────────────────────────────────────────────────
        private DrawingColors GetDrawingColor(string colorName)
        {
            if (Enum.TryParse(colorName, out DrawingColors color))
                return color;
            return DrawingColors.Green;
        }

        private Vector Neg(Vector v) => new Vector(-v.X, -v.Y, -v.Z);
    }
}