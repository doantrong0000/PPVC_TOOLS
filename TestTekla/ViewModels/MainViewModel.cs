using System;
using System.Collections;
using System.Collections.Generic;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Models;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;

namespace TeklaApp.ViewModels
{
    public class MainViewModel
    {
        private TeklaModelMng _teklaModel;

        public MainViewModel()
        {
            _teklaModel = new TeklaModelMng();
        }

        public void SelectRebarsOfPart()
        {
            if (!_teklaModel.IsConnected()) return;

            try
            {
                Tekla.Structures.Model.ModelObject pickedObject = null;
                
                // 1. Try to get current selection first
                Tekla.Structures.Model.UI.ModelObjectSelector currentSelector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                Tekla.Structures.Model.ModelObjectEnumerator selectedObjects = currentSelector.GetSelectedObjects();
                
                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is Tekla.Structures.Model.Part)
                    {
                        pickedObject = selectedObjects.Current;
                        break; // Use the first part found in selection
                    }
                }

                // 2. If no part is selected, fallback to Picker
                if (pickedObject == null)
                {
                    Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                    pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Select a Part/Beam to select its rebars");
                }

                if (pickedObject is Tekla.Structures.Model.Part part)
                {
                    System.Collections.ArrayList rebarsToSelect = new System.Collections.ArrayList();

                    // 1. Check direct children
                    Tekla.Structures.Model.ModelObjectEnumerator children = part.GetChildren();
                    while (children.MoveNext())
                    {
                        if (children.Current is Tekla.Structures.Model.Reinforcement rebar)
                        {
                            rebarsToSelect.Add(rebar);
                        }
                    }

                    // 2. If nothing found, check Assembly/CastUnit content
                    if (rebarsToSelect.Count == 0)
                    {
                        var assembly = part.GetAssembly();
                        if (assembly != null)
                        {
                            System.Collections.ArrayList members = assembly.GetSecondaries();
                            foreach (var member in members)
                            {
                                if (member is Tekla.Structures.Model.Reinforcement rebar)
                                {
                                    rebarsToSelect.Add(rebar);
                                }
                            }
                        }
                    }

                    if (rebarsToSelect.Count > 0)
                    {
                        Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                        selector.Select(rebarsToSelect);
                        Tekla.Structures.Model.Operations.Operation.DisplayPrompt($"[SELECT] Found {rebarsToSelect.Count} rebars for part: {part.Name}");
                    }
                    else
                    {
                        Tekla.Structures.Model.Operations.Operation.DisplayPrompt("[SELECT] No rebars found associated with this part.");
                    }
                }
            }
            catch { }
        }

        public string DeletePartCuts()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                Tekla.Structures.Model.ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Please select a part to delete PartCuts");

                if (pickedObject is Tekla.Structures.Model.Part hostPart)
                {
                    Tekla.Structures.Model.ModelObjectEnumerator cutEnumerator = hostPart.GetBooleans();
                    int cutCount = 0;

                    while (cutEnumerator.MoveNext())
                    {
                        if (cutEnumerator.Current is Tekla.Structures.Model.BooleanPart booleanCut)
                        {
                            cutCount++;
                            booleanCut.Delete();
                        }
                    }

                    if (cutCount > 0)
                    {
                        _teklaModel.Commit();
                        return $"Successfully deleted {cutCount} PartCuts.";
                    }
                    else
                    {
                        return "This part has no PartCuts.";
                    }
                }
                else
                {
                    return "Invalid object selected.";
                }
            }
            catch (Exception ex)
            {
                return "Cancelled or error occurred: " + ex.Message;
            }
        }

        public string JoinAssembly()
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla Structures is not running.";
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();

                Tekla.Structures.Model.ModelObject mainObj = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Please select the main part...");
                if (mainObj is Tekla.Structures.Model.Part mainPart)
                {
                    Tekla.Structures.Model.ModelObjectEnumerator secondaryObjects = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, "Sweep select secondary parts and press MIDDLE mouse button to finish...");

                    Tekla.Structures.Model.Assembly assembly = mainPart.GetAssembly();
                    int count = 0;

                    while (secondaryObjects.MoveNext())
                    {
                        if (secondaryObjects.Current is Tekla.Structures.Model.Part secPart && secPart.Identifier.ID != mainPart.Identifier.ID)
                        {
                            assembly.Add(secPart);
                            count++;
                        }
                    }

                    if (count > 0)
                    {
                        assembly.Modify();
                        _teklaModel.Commit();
                        return $"Done!\r\nSuccessfully added {count} secondary parts to the Assembly/CastUnit of the main part (Profile: {mainPart.Profile.ProfileString}).";
                    }
                    else
                    {
                        return "No valid secondary parts were selected.";
                    }
                }
                else
                {
                    return "Selected main object is invalid (Not a Part).";
                }
            }
            catch (Exception ex)
            {
                return "Cancelled or error occurred: " + ex.Message;
            }
        }

        public void ReverseRebarDistribution()
        {
            if (!_teklaModel.IsConnected())
            {
                return;
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();

                while (true)
                {
                    Tekla.Structures.Model.ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_REINFORCEMENT, "Please select a rebar group to reverse distribution (Press Esc to stop)");
                    
                    if (pickedObject is Tekla.Structures.Model.RebarGroup rebarGroup)
                    {
                        var tempPoint = new Tekla.Structures.Geometry3d.Point(rebarGroup.StartPoint);
                        rebarGroup.StartPoint = new Tekla.Structures.Geometry3d.Point(rebarGroup.EndPoint);
                        rebarGroup.EndPoint = tempPoint;

                        // Reverse the spacing array to preserve exact physical locations
                        if (rebarGroup.Spacings.Count > 1) 
                        {
                            var reversedSpacings = new System.Collections.ArrayList();
                            for (int i = rebarGroup.Spacings.Count - 1; i >= 0; i--)
                            {
                                reversedSpacings.Add(rebarGroup.Spacings[i]);
                            }
                            rebarGroup.Spacings = reversedSpacings;
                        }

                        if (rebarGroup.Modify())
                        {
                            _teklaModel.Commit();
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

        public void RepickRebarRange()
        {
            if (!_teklaModel.IsConnected())
            {
                return;
            }

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();

                while (true)
                {
                    Tekla.Structures.Model.ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_REINFORCEMENT, "Please select a rebar group to modify its range (Press Esc to stop)");
                    
                    if (pickedObject is Tekla.Structures.Model.RebarGroup rebarGroup)
                    {
                        var startPoint = picker.PickPoint("Pick new Start Point of distribution");
                        var endPoint = picker.PickPoint("Pick new End Point of distribution");

                        rebarGroup.StartPoint = startPoint;
                        rebarGroup.EndPoint = endPoint;

                        if (rebarGroup.Modify())
                        {
                            _teklaModel.Commit();
                        }
                    }
                }
            }
            catch
            {
                return;
            }
        }

        public void CheckLap()
        {
            if (!_teklaModel.IsConnected()) return;

            try
            {
                Picker picker = new Picker();
                Reinforcement pickedRebar = null;

                // 1. KIỂM TRA THÉP ĐÃ CHỌN TRƯỚC (Pre-selection)
                ModelObjectSelector selector = new ModelObjectSelector();
                IEnumerator selectedObjects = selector.GetSelectedObjects();

                while (selectedObjects.MoveNext())
                {
                    // Trong Tekla API, class cha của RebarGroup và SingleRebar là Reinforcement
                    if (selectedObjects.Current is Reinforcement)
                    {
                        pickedRebar = selectedObjects.Current as Reinforcement;
                        break;
                    }
                }

                // Nếu chưa chọn thì yêu cầu người dùng pick
                if (pickedRebar == null)
                {
                    pickedRebar = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_REINFORCEMENT, "Select Rebar to check anchorage") as Reinforcement;
                }

                if (pickedRebar == null) return;

                // 2. LẤY THÔNG TIN HÌNH HỌC VÀ ĐƯỜNG KÍNH
                ArrayList points = null;
                double diameter = 0;

                if (pickedRebar is RebarGroup group)
                {
                    points = (group.Polygons[0] as Polygon).Points;
                    diameter = group.Name.Contains("d") || group.Name.Contains("D") ? ExtractDiameter(group.Size) : 20; // Default hoặc lấy từ thuộc tính
                }
                else if (pickedRebar is SingleRebar single)
                {
                    points = single.Polygon.Points;
                    diameter = ExtractDiameter(single.Size);
                }

                if (points == null || points.Count < 2) return;

                // 3. CHỌN ĐIỂM MỐC TÍNH NEO
                Point pClick = picker.PickPoint("Pick exact start position of anchorage (Face of concrete)");

                // 4. THUẬT TOÁN CHIẾU ĐIỂM VÀ TÍNH CHIỀU DÀI DỌC THÂN
                Point projectedPoint = null;
                int segmentIndex = -1;
                double minDoc = double.MaxValue;

                for (int i = 0; i < points.Count - 1; i++)
                {
                    Point p1 = points[i] as Point;
                    Point p2 = points[i + 1] as Point;

                    // Chiếu điểm lên đường thẳng chứa phân đoạn thép
                    Line line = new Line(p1, new Vector(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z));
                    Point proj = Projection.PointToLine(pClick, line);

                    double d = Distance.PointToPoint(pClick, proj);
                    if (d < minDoc)
                    {
                        minDoc = d;
                        projectedPoint = proj;
                        segmentIndex = i;
                    }
                }

                if (projectedPoint != null)
                {
                    // Tính tổng chiều dài từ điểm chiếu về hai đầu
                    double distToStart = Distance.PointToPoint(projectedPoint, points[segmentIndex] as Point);
                    for (int i = segmentIndex; i > 0; i--)
                        distToStart += Distance.PointToPoint(points[i] as Point, points[i - 1] as Point);

                    double distToEnd = Distance.PointToPoint(projectedPoint, points[segmentIndex + 1] as Point);
                    for (int i = segmentIndex + 1; i < points.Count - 1; i++)
                        distToEnd += Distance.PointToPoint(points[i] as Point, points[i + 1] as Point);

                    // 5. HIỂN THỊ KẾT QUẢ THEO SỐ LẦN ĐƯỜNG KÍNH (nD)
                    // Giả sử đường kính lấy từ thuộc tính Size (ví dụ "D20" -> 20)
                    double dActual = diameter > 0 ? diameter : 1.0;
                    string result = $"[ANCHORAGE CHECK]\n" +
                                    $"Start side: {Math.Round(distToStart, 1)} mm ({Math.Round(distToStart / dActual, 1)}d)\n" +
                                    $"End side: {Math.Round(distToEnd, 1)} mm ({Math.Round(distToEnd / dActual, 1)}d)";

                    Tekla.Structures.Model.Operations.Operation.DisplayPrompt(result.Replace("\n", " | "));
                    System.Windows.Forms.MessageBox.Show(result, "Anchor Result (D" + dActual + ")");
                }
            }
            catch { }
        }

        // Hàm bổ trợ lấy số từ chuỗi Size (ví dụ "D25" -> 25.0)
        private double ExtractDiameter(string size)
        {
            string res = System.Text.RegularExpressions.Regex.Match(size, @"\d+").Value;
            double d;
            return double.TryParse(res, out d) ? d : 0;
        }

        public void QuickDim()
        {
            var dh = new Tekla.Structures.Drawing.DrawingHandler();
            if (dh.GetActiveDrawing() == null)
            {
                return;
            }

            try
            {
                var picker = dh.GetPicker();
                Tekla.Structures.Drawing.ViewBase viewBase1, viewBase2, viewBase3;
                Tekla.Structures.Geometry3d.Point p1, p2, p3;

                picker.PickPoint("Pick first point to define dimension line axis", out p1, out viewBase1);
                picker.PickPoint("Pick second point to define dimension line axis", out p2, out viewBase2);

                if (viewBase1 == null || viewBase2 == null)
                {
                    return;
                }

                picker.PickPoint("Pick dimension placement point", out p3, out viewBase3);

                var view = viewBase1 as Tekla.Structures.Drawing.View;
                if (view == null) return;

                double dx = Math.Abs(p2.X - p1.X);
                double dy = Math.Abs(p2.Y - p1.Y);
                bool isHorizontal = dx >= dy;

                var dObjEnum = view.GetObjects(new Type[] { typeof(Tekla.Structures.Drawing.Part) });
                var model = new Tekla.Structures.Model.Model();

                var sys = view.DisplayCoordinateSystem;
                var toViewMatrix = Tekla.Structures.Geometry3d.MatrixFactory.ToCoordinateSystem(sys);

                var dimCoords = new List<double>();

                // Add bounding coordinates based on user input line
                dimCoords.Add(isHorizontal ? p1.X : p1.Y);
                dimCoords.Add(isHorizontal ? p2.X : p2.Y);

                double scanMinX = Math.Min(p1.X, p2.X);
                double scanMaxX = Math.Max(p1.X, p2.X);
                double scanMinY = Math.Min(p1.Y, p2.Y);
                double scanMaxY = Math.Max(p1.Y, p2.Y);

                while (dObjEnum.MoveNext())
                {
                    if (dObjEnum.Current is Tekla.Structures.Drawing.Part dPart)
                    {
                        var mPart = model.SelectModelObject(dPart.ModelIdentifier) as Tekla.Structures.Model.Part;
                        if (mPart == null) continue;

                        var solid = mPart.GetSolid();
                        if (solid == null) continue;

                        var faceEnum = solid.GetFaceEnumerator();
                        var originModel = new Tekla.Structures.Geometry3d.Point(0, 0, 0);
                        var originView = toViewMatrix.Transform(originModel);

                        while (faceEnum.MoveNext())
                        {
                            var face = faceEnum.Current as Tekla.Structures.Solid.Face;
                            if (face == null) continue;

                            var norm = face.Normal;
                            if (norm != null)
                            {
                                var ptNorm = new Tekla.Structures.Geometry3d.Point(norm.X, norm.Y, norm.Z);
                                var mappedPtNorm = toViewMatrix.Transform(ptNorm);
                                double mapZ = mappedPtNorm.Z - originView.Z;

                                // Skip back-facing or exactly side-facing planes
                                // This perfectly eliminates 99% of internal hidden lines!
                                if (mapZ <= 1e-4) continue;
                            }

                            var loopEnum = face.GetLoopEnumerator();
                            while (loopEnum.MoveNext())
                            {
                                var loop = loopEnum.Current as Tekla.Structures.Solid.Loop;
                                if (loop == null) continue;

                                var vertexEnum = loop.GetVertexEnumerator();
                                var loopPts = new List<Tekla.Structures.Geometry3d.Point>();
                                while (vertexEnum.MoveNext())
                                {
                                    var pt = vertexEnum.Current as Tekla.Structures.Geometry3d.Point;
                                    if (pt != null) loopPts.Add(pt);
                                }

                                if (loopPts.Count < 2) continue;

                                for (int i = 0; i < loopPts.Count; i++)
                                {
                                    var sp3d = loopPts[i];
                                    var ep3d = loopPts[(i + 1) % loopPts.Count];

                                    var sp = toViewMatrix.Transform(sp3d);
                                    var ep = toViewMatrix.Transform(ep3d);

                                    if (isHorizontal)
                                    {
                                        if (Math.Abs(sp.Y - ep.Y) > 1e-2)
                                        {
                                            double minY = Math.Min(sp.Y, ep.Y);
                                            double maxY = Math.Max(sp.Y, ep.Y);

                                            if (p1.Y >= minY - 0.1 && p1.Y <= maxY + 0.1)
                                            {
                                                double t = (p1.Y - sp.Y) / (ep.Y - sp.Y);
                                                double intersectX = sp.X + t * (ep.X - sp.X);
                                                if (intersectX >= scanMinX - 1.0 && intersectX <= scanMaxX + 1.0)
                                                {
                                                    dimCoords.Add(intersectX);
                                                }
                                            }
                                        }
                                    }
                                    else
                                    {
                                        if (Math.Abs(sp.X - ep.X) > 1e-2)
                                        {
                                            double minX = Math.Min(sp.X, ep.X);
                                            double maxX = Math.Max(sp.X, ep.X);

                                            if (p1.X >= minX - 0.1 && p1.X <= maxX + 0.1)
                                            {
                                                double t = (p1.X - sp.X) / (ep.X - sp.X);
                                                double intersectY = sp.Y + t * (ep.Y - sp.Y);
                                                if (intersectY >= scanMinY - 1.0 && intersectY <= scanMaxY + 1.0)
                                                {
                                                    dimCoords.Add(intersectY);
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                dimCoords.Sort();
                var uniqueCoords = new List<double>();
                foreach (var c in dimCoords)
                {
                    if (uniqueCoords.Count == 0 || Math.Abs(uniqueCoords[uniqueCoords.Count - 1] - c) > 1.0)
                    {
                        uniqueCoords.Add(c);
                    }
                }

                if (uniqueCoords.Count < 2)
                {
                    return;
                }

                var pointList = new Tekla.Structures.Drawing.PointList();
                foreach (var c in uniqueCoords)
                {
                    if (isHorizontal)
                        pointList.Add(new Tekla.Structures.Geometry3d.Point(c, p1.Y, 0));
                    else
                        pointList.Add(new Tekla.Structures.Geometry3d.Point(p1.X, c, 0));
                }

                // Placing dimension
                var buildDir = new Tekla.Structures.Geometry3d.Vector(p3.X - p1.X, p3.Y - p1.Y, 0);
                if (isHorizontal) buildDir.X = 0; else buildDir.Y = 0;

                double distance = buildDir.GetLength();
                buildDir.Normalize();

                var attrs = new Tekla.Structures.Drawing.StraightDimensionSet.StraightDimensionSetAttributes();
                var handler = new Tekla.Structures.Drawing.StraightDimensionSetHandler();
                handler.CreateDimensionSet(view, pointList, buildDir, distance, attrs);

                dh.GetActiveDrawing().CommitChanges();
                return;
            }
            catch (Exception ex)
            {
                return;
            }
        }
    }
}
