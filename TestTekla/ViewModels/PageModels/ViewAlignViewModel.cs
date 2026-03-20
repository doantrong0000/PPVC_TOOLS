using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Geometry3d;
using TSM = Tekla.Structures.Model;
using TSD = Tekla.Structures.Drawing;
using TSDUI = Tekla.Structures.Drawing.UI;
using Tekla.Structures.Model.UI;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    /// <summary>
    /// Trục cần align
    /// </summary>
    public enum AlignAxis
    {
        X,
        Y,
        Z
    }

    /// <summary>
    /// Chế độ chọn đối tượng
    /// </summary>
    public enum AlignTarget
    {
        Points,     // Dùng Picker pick các điểm polygon của object
        Objects,    // Dùng Picker pick objects rồi move toàn bộ
        DrawingViews // Căn chỉnh các View trong bản vẽ
    }

    /// <summary>
    /// ViewModel cho tính năng View Align.
    /// Cho phép chọn 1 điểm mốc (Reference Point), sau đó pick các điểm/object khác
    /// và đưa tất cả về cùng tọa độ trên trục đã chọn (X, Y, hoặc Z).
    /// </summary>
    public class ViewAlignViewModel
    {
        private TeklaModelMng _teklaModel;

        public Point ReferencePoint { get; private set; }

        public ViewAlignViewModel()
        {
            _teklaModel = new TeklaModelMng();
        }

        /// <summary>
        /// Align từng polygon point (handle point) của rebar/part về cùng tọa độ trục.
        /// Workflow:
        ///   1. Pick 1 điểm mốc (Reference) → lấy tọa độ trục cần align
        ///   2. Pick 1 rebar/object để xác định đối tượng cần chỉnh
        public string AlignObjectPoints(AlignAxis axis, out int alignedCount)
        {
            alignedCount = 0;

            if (!_teklaModel.IsConnected())
                return "Error: Tekla Structures is not running.";

            try
            {
                Picker picker = new Picker();

                // Step 1: Pick Reference Point
                Point refPoint = picker.PickPoint("EDIT POINTS: Pick Reference Point");
                if (refPoint == null)
                    return "Cancelled: No reference point picked.";

                ReferencePoint = refPoint;
                double refValue = GetAxisValue(refPoint, axis);

                // Step 2: Pick the rebar object to edit
                TSM.ModelObject pickedObj = picker.PickObject(
                    Picker.PickObjectEnum.PICK_ONE_OBJECT,
                    $"Pick rebar/object to edit points → align {axis} = {refValue:F1}");

                if (pickedObj == null)
                    return "Cancelled: No object picked.";

                // Lấy danh sách polygon points của object
                ArrayList polygonPoints = GetAllPolygonPoints(pickedObj);
                if (polygonPoints == null || polygonPoints.Count == 0)
                    return "Error: Selected object has no editable polygon points.";

                int pointsAligned = 0;

                // Step 3: Loop pick các điểm cần align
                while (true)
                {
                    try
                    {
                        Point pickedPoint = picker.PickPoint(
                            $"Pick handle point to align → {axis} = {refValue:F1}  (Esc to finish, đã align {pointsAligned} point(s))");

                        if (pickedPoint == null) break;

                        // Tìm polygon point gần nhất với điểm đã pick
                        Point nearest = FindNearestPolygonPoint(polygonPoints, pickedPoint);
                        if (nearest != null)
                        {
                            if (SetAxisValue(nearest, axis, refValue))
                            {
                                pointsAligned++;

                                // Modify ngay sau mỗi lần pick để user thấy kết quả realtime
                                if (pickedObj is TSM.RebarGroup rg) rg.Modify();
                                else if (pickedObj is TSM.SingleRebar sr) sr.Modify();
                                else if (pickedObj is TSM.Part pt) pt.Modify();

                                _teklaModel.Commit();

                                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                                    $"✓ Aligned point → {axis} = {refValue:F1}  (total: {pointsAligned})");
                            }
                            else
                            {
                                Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                                    "Point already at target value. Pick another point.");
                            }
                        }
                        else
                        {
                            Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                                "No nearby polygon point found. Try picking closer to a handle.");
                        }
                    }
                    catch
                    {
                        // Esc pressed → exit loop
                        break;
                    }
                }

                alignedCount = pointsAligned;

                if (pointsAligned > 0)
                    return $"Done! Aligned {pointsAligned} point(s) to {axis} = {refValue:F1}";
                else
                    return "No points were aligned.";
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name.Contains("Picker") || ex.Message.Contains("interrupt"))
                    return "Cancelled by user.";
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Lấy tất cả polygon points từ object (dạng reference, modify trực tiếp sẽ thay đổi object)
        /// </summary>
        private ArrayList GetAllPolygonPoints(TSM.ModelObject obj)
        {
            ArrayList points = new ArrayList();

            if (obj is TSM.RebarGroup rebarGroup)
            {
                if (rebarGroup.Polygons != null)
                {
                    foreach (TSM.Polygon polygon in rebarGroup.Polygons)
                    {
                        foreach (Point pt in polygon.Points)
                        {
                            points.Add(pt);
                        }
                    }
                }
            }
            else if (obj is TSM.SingleRebar singleRebar)
            {
                if (singleRebar.Polygon != null)
                {
                    foreach (Point pt in singleRebar.Polygon.Points)
                    {
                        points.Add(pt);
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// Tìm polygon point gần nhất với điểm đã pick (trong khoảng tolerance)
        /// </summary>
        private Point FindNearestPolygonPoint(ArrayList polygonPoints, Point pickedPoint)
        {
            Point nearest = null;
            double minDist = double.MaxValue;

            foreach (Point pt in polygonPoints)
            {
                double dx = pt.X - pickedPoint.X;
                double dy = pt.Y - pickedPoint.Y;
                double dz = pt.Z - pickedPoint.Z;
                double dist = Math.Sqrt(dx * dx + dy * dy + dz * dz);

                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = pt;
                }
            }

            // Tolerance: nếu điểm pick quá xa (> 500mm) thì bỏ qua
            if (minDist > 500.0)
                return null;

            return nearest;
        }

        /// <summary>
        /// Align theo cách di chuyển toàn bộ object (dùng cho Part, cấu kiện).
        /// Workflow:
        ///   1. Pick 1 điểm mốc
        ///   2. Pick nhiều object
        ///   3. Move từng object về tọa độ trục = refValue
        /// </summary>
        public string AlignByMovingObjects(AlignAxis axis, out int alignedCount)
        {
            alignedCount = 0;

            if (!_teklaModel.IsConnected())
                return "Error: Tekla Structures is not running.";

            try
            {
                Picker picker = new Picker();

                // Step 1: Pick Reference Point
                Point refPoint = picker.PickPoint("VIEW ALIGN (Move): Pick Reference Point");
                if (refPoint == null)
                    return "Cancelled: No reference point picked.";

                ReferencePoint = refPoint;
                double refValue = GetAxisValue(refPoint, axis);

                // Step 2: Pick objects to move
                TSM.ModelObjectEnumerator selectedObjects = picker.PickObjects(
                    Picker.PickObjectsEnum.PICK_N_OBJECTS,
                    $"Pick objects to MOVE to {axis} = {refValue:F1}  (Middle click to finish)");

                if (selectedObjects == null)
                    return "Cancelled: No objects picked.";

                int movedCount = 0;

                while (selectedObjects.MoveNext())
                {
                    TSM.ModelObject obj = selectedObjects.Current as TSM.ModelObject;

                    if (obj is TSM.Part part)
                    {
                        if (AlignPartByMoving(part, axis, refValue))
                            movedCount++;
                    }
                    else if (obj is TSM.RebarGroup rebarGroup)
                    {
                        if (AlignRebarGroupByMoving(rebarGroup, axis, refValue))
                            movedCount++;
                    }
                    else if (obj is TSM.SingleRebar singleRebar)
                    {
                        if (AlignSingleRebarByMoving(singleRebar, axis, refValue))
                            movedCount++;
                    }
                }

                if (movedCount > 0)
                {
                    _teklaModel.Commit();
                    alignedCount = movedCount;
                    return $"Done! Moved {movedCount} object(s) → {axis} = {refValue:F1}";
                }
                else
                {
                    return "No objects were moved.";
                }
            }
            catch (Exception ex)
            {
                if (ex.GetType().Name.Contains("Picker") || ex.Message.Contains("interrupt"))
                    return "Cancelled by user.";
                return "Error: " + ex.Message;
            }
        }


        #region Private Methods - Align by moving entire object

        /// <summary>
        /// Move Part bằng cách dịch chuyển Start/EndPoint (chỉ hỗ trợ Beam và các Part có Start/EndPoint)
        /// </summary>
        private bool AlignPartByMoving(TSM.Part part, AlignAxis axis, double refValue)
        {
            // Beam (Dầm, Cột, etc.) có StartPoint và EndPoint
            if (part is TSM.Beam beam)
            {
                if (beam.StartPoint == null) return false;

                double currentValue = GetAxisValue(beam.StartPoint, axis);
                double delta = refValue - currentValue;

                if (Math.Abs(delta) < 0.01) return false;

                ApplyOffset(beam.StartPoint, axis, delta);
                if (beam.EndPoint != null)
                    ApplyOffset(beam.EndPoint, axis, delta);

                return beam.Modify();
            }

            // ContourPlate, Slab, etc. có Contour with Points
            if (part is TSM.ContourPlate plate)
            {
                if (plate.Contour == null || plate.Contour.ContourPoints.Count == 0)
                    return false;

                // Lấy điểm đầu tiên để tính delta
                TSM.ContourPoint firstCp = plate.Contour.ContourPoints[0] as TSM.ContourPoint;
                if (firstCp == null) return false;

                double currentValue = GetAxisValue(new Point(firstCp.X, firstCp.Y, firstCp.Z), axis);
                double delta = refValue - currentValue;

                if (Math.Abs(delta) < 0.01) return false;

                foreach (TSM.ContourPoint cp in plate.Contour.ContourPoints)
                {
                    Point tempPt = new Point(cp.X, cp.Y, cp.Z);
                    ApplyOffset(tempPt, axis, delta);
                    cp.X = tempPt.X;
                    cp.Y = tempPt.Y;
                    cp.Z = tempPt.Z;
                }

                return plate.Modify();
            }

            return false;
        }

        /// <summary>
        /// Move RebarGroup bằng cách dịch chuyển toàn bộ polygon points cùng 1 offset
        /// </summary>
        private bool AlignRebarGroupByMoving(TSM.RebarGroup rebarGroup, AlignAxis axis, double refValue)
        {
            if (rebarGroup.Polygons == null || rebarGroup.Polygons.Count == 0)
                return false;

            // Lấy điểm đầu tiên làm reference cho offset
            Point firstPoint = null;
            foreach (TSM.Polygon polygon in rebarGroup.Polygons)
            {
                if (polygon.Points.Count > 0)
                {
                    firstPoint = polygon.Points[0] as Point;
                    break;
                }
            }
            if (firstPoint == null) return false;

            double currentValue = GetAxisValue(firstPoint, axis);
            double delta = refValue - currentValue;

            if (Math.Abs(delta) < 0.01) return false;

            // Offset start/end points
            if (rebarGroup.StartPoint != null)
                ApplyOffset(rebarGroup.StartPoint, axis, delta);
            if (rebarGroup.EndPoint != null)
                ApplyOffset(rebarGroup.EndPoint, axis, delta);

            // Offset all polygon points
            foreach (TSM.Polygon polygon in rebarGroup.Polygons)
            {
                foreach (Point pt in polygon.Points)
                {
                    ApplyOffset(pt, axis, delta);
                }
            }

            return rebarGroup.Modify();
        }

        /// <summary>
        /// Move SingleRebar bằng cách dịch chuyển toàn bộ polygon points cùng 1 offset
        /// </summary>
        private bool AlignSingleRebarByMoving(TSM.SingleRebar singleRebar, AlignAxis axis, double refValue)
        {
            if (singleRebar.Polygon == null || singleRebar.Polygon.Points.Count == 0)
                return false;

            Point firstPoint = singleRebar.Polygon.Points[0] as Point;
            if (firstPoint == null) return false;

            double currentValue = GetAxisValue(firstPoint, axis);
            double delta = refValue - currentValue;

            if (Math.Abs(delta) < 0.01) return false;

            foreach (Point pt in singleRebar.Polygon.Points)
            {
                ApplyOffset(pt, axis, delta);
            }

            return singleRebar.Modify();
        }

        /// <summary>
        /// Căn chỉnh các View trong Drawing.
        /// Có 2 chế độ:
        ///   1. Align by Origin: Căn chỉnh theo điểm gốc (thường là góc dưới trái) của View.
        ///   2. Align by COG: Căn chỉnh theo trọng tâm của các cấu kiện có trong View.
        /// </summary>
        public string AlignDrawingViews(AlignAxis axis, bool alignByCenter, out int alignedCount)
        {
            alignedCount = 0;
            if (axis == AlignAxis.Z)
                return "Error: Z-axis alignment is not supported for regular drawing views (2D layout).";

            try
            {
                TSD.DrawingHandler drawingHandler = new TSD.DrawingHandler();
                if (drawingHandler.GetActiveDrawing() == null)
                    return "Error: No active drawing open.";

                dynamic drawingPicker = drawingHandler.GetPicker();

                // Step 1: Pick Reference Point
                var pickPointResult = drawingPicker.PickPoint("VIEW ALIGN (Drawing): Pick Reference Point");
                if (pickPointResult == null)
                    return "Cancelled: No reference point picked.";

                Point refPoint = pickPointResult.Item1;
                ReferencePoint = refPoint;
                double refValue = GetAxisValue(refPoint, axis);

                // Cách tiếp cận mới: Ưu tiên lấy các đối tượng đang được chọn (Selected) trước
                // Điều này tránh các vấn đề về phiên bản API Picker (inaccessible) trong Tekla 2024/2025
                TSD.DrawingObjectEnumerator selectedObjects = drawingHandler.GetDrawingObjectSelector().GetSelected();

                if (selectedObjects.GetSize() == 0)
                {
                    // Nếu không có gì được chọn trước, thử dùng Picker (có thể bị lỗi ở 2024/2025)
                    try
                    {
                        var pickObjectsResult = drawingPicker.PickObjects("Pick views to align (Middle click to finish)");
                        if (pickObjectsResult == null)
                            return "Cancelled: No objects picked.";

                        // Xử lý cả trường hợp Tuple và DrawingObjectEnumerator trực tiếp
                        if (pickObjectsResult is TSD.DrawingObjectEnumerator enu)
                            selectedObjects = enu;
                        else
                            selectedObjects = ((dynamic)pickObjectsResult).Item1;
                    }
                    catch (Exception ex)
                    {
                        return "Error in selection: Please select objects in drawing BEFORE running the tool if the picker fails. Error: " + ex.Message;
                    }
                }

                if (selectedObjects.GetSize() == 0)
                    return "No objects selected to align.";

                int movedCount = 0;
                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is TSD.View currentView)
                    {
                        Point currentOrigin = currentView.Origin;
                        double shiftValue = 0;

                        if (alignByCenter)
                        {
                            Point contentCenter = GetViewProjectedCenter(currentView);
                            if (contentCenter != null)
                            {
                                double currentCenterVal = GetAxisValue(contentCenter, axis);
                                shiftValue = refValue - currentCenterVal;
                            }
                            else
                            {
                                // Fallback to origin if no objects found in view
                                shiftValue = refValue - GetAxisValue(currentOrigin, axis);
                            }
                        }
                        else
                        {
                            shiftValue = refValue - GetAxisValue(currentOrigin, axis);
                        }

                        if (Math.Abs(shiftValue) > 0.01)
                        {
                            Point newOrigin = new Point(currentOrigin.X, currentOrigin.Y, currentOrigin.Z);
                            ApplyOffset(newOrigin, axis, shiftValue);
                            currentView.Origin = newOrigin;
                            if (currentView.Modify())
                                movedCount++;
                        }
                    }
                }

                if (movedCount > 0)
                {
                    alignedCount = movedCount;
                    return $"Done! Aligned {movedCount} drawing view(s) → {axis} = {refValue:F1}";
                }
                else
                {
                    return "No drawing views were moved.";
                }
            }
            catch (Exception ex)
            {
                if (ex.Message.Contains("interrupt") || ex.GetType().Name.Contains("Picker"))
                    return "Cancelled by user.";
                return "Error: " + ex.Message;
            }
        }

        /// <summary>
        /// Tính toán trung tâm của tất cả các Part trong View (sau khi chiếu lên mặt phẳng View).
        /// Trả về điểm ở hệ tọa độ Drawing (Sheet space).
        /// </summary>
        private Point GetViewProjectedCenter(TSD.View view)
        {
            var objects = view.GetObjects();
            List<Point> modelCogs = new List<Point>();
            TSM.Model model = new TSM.Model();

            while (objects.MoveNext())
            {
                if (objects.Current is TSD.Part drawingPart)
                {
                    var modelObj = model.SelectModelObject(drawingPart.ModelIdentifier) as TSM.Part;
                    if (modelObj != null)
                    {
                        // Lấy solid để lấy trung tâm bounding box chính xác hơn
                        TSM.Solid solid = modelObj.GetSolid();
                        if (solid != null)
                        {
                            Point center = new Point(
                                (solid.MinimumPoint.X + solid.MaximumPoint.X) / 2.0,
                                (solid.MinimumPoint.Y + solid.MaximumPoint.Y) / 2.0,
                                (solid.MinimumPoint.Z + solid.MaximumPoint.Z) / 2.0
                            );
                            modelCogs.Add(center);
                        }
                        else
                        {
                            // Fallback if no solid (unlikely for parts with geometry)
                            // or use part insertion points if beam
                            if (modelObj is TSM.Beam beam)
                            {
                                modelCogs.Add(new Point(
                                    (beam.StartPoint.X + beam.EndPoint.X) / 2.0,
                                    (beam.StartPoint.Y + beam.EndPoint.Y) / 2.0,
                                    (beam.StartPoint.Z + beam.EndPoint.Z) / 2.0
                                ));
                            }
                        }
                    }
                }
            }

            if (modelCogs.Count == 0) return null;

            // Lấy trung tâm trung bình của các cấu kiện
            Point avgModelCog = new Point(
                modelCogs.Average(p => p.X),
                modelCogs.Average(p => p.Y),
                modelCogs.Average(p => p.Z)
            );

            // Chuyển đổi từ Model space sang Drawing View space
            Matrix toView = MatrixFactory.ToCoordinateSystem(view.DisplayCoordinateSystem);
            Point projectedCenter = toView.Transform(avgModelCog);

            return projectedCenter;
        }

        #endregion

        #region Utility Methods

        private double GetAxisValue(Point point, AlignAxis axis)
        {
            switch (axis)
            {
                case AlignAxis.X: return point.X;
                case AlignAxis.Y: return point.Y;
                case AlignAxis.Z: return point.Z;
                default: return 0;
            }
        }

        /// <summary>
        /// Set giá trị trục cho point, trả về true nếu giá trị thay đổi
        /// </summary>
        private bool SetAxisValue(Point point, AlignAxis axis, double value)
        {
            double currentValue = GetAxisValue(point, axis);
            if (Math.Abs(currentValue - value) < 0.01)
                return false;

            switch (axis)
            {
                case AlignAxis.X: point.X = value; break;
                case AlignAxis.Y: point.Y = value; break;
                case AlignAxis.Z: point.Z = value; break;
            }
            return true;
        }

        private void ApplyOffset(Point point, AlignAxis axis, double delta)
        {
            switch (axis)
            {
                case AlignAxis.X: point.X += delta; break;
                case AlignAxis.Y: point.Y += delta; break;
                case AlignAxis.Z: point.Z += delta; break;
            }
        }

        #endregion
    }
}
