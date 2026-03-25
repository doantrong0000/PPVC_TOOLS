using Fusion.Data.Query;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Policy;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Models;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.ProgressBar;
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

        public void DeletePartCuts()
        {
            if (!_teklaModel.IsConnected())
            {
                return;
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
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }

        public void JoinAssembly()
        {
            if (!_teklaModel.IsConnected())
            {
                return;
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
                    }
                }
            }
            catch (Exception)
            {
                return;
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

        public void DrawOpeningDiagonal()
        {
            var dh = new Tekla.Structures.Drawing.DrawingHandler();
            if (dh.GetActiveDrawing() == null) return;

            try
            {
                var picker = dh.GetPicker();

                while (true)
                {
                    var result1 = picker.PickPoint("Pick first corner of opening");
                    var result2 = picker.PickPoint("Pick opposite corner of opening");

                    Tekla.Structures.Geometry3d.Point p1 = result1.Item1;
                    Tekla.Structures.Drawing.ViewBase view = result1.Item2;
                    Tekla.Structures.Geometry3d.Point p2 = result2.Item1;

                    // Tạo 4 góc từ 2 điểm đối diện
                    var corner1 = new Tekla.Structures.Geometry3d.Point(p1.X, p1.Y, 0);
                    var corner2 = new Tekla.Structures.Geometry3d.Point(p2.X, p2.Y, 0);
                    var corner3 = new Tekla.Structures.Geometry3d.Point(p1.X, p2.Y, 0);
                    var corner4 = new Tekla.Structures.Geometry3d.Point(p2.X, p1.Y, 0);

                    // Vẽ đường chéo 1
                    var line1 = new Tekla.Structures.Drawing.Line(view, corner1, corner2);
                    line1.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DashedLine;
                    line1.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                    line1.Insert();

                    // Vẽ đường chéo 2
                    var line2 = new Tekla.Structures.Drawing.Line(view, corner3, corner4);
                    line2.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DashedLine;
                    line2.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                    line2.Insert();

                    dh.GetActiveDrawing().CommitChanges();
                }
            }
            catch { return; }
        }

        /// <summary>
        /// Tự động quét toàn bộ cấu kiện trong drawing, phát hiện lỗ mở (BooleanPart)
        /// và vẽ đường chéo (X) ký hiệu lỗ mở.
        /// Hỗ trợ lỗ mở ở góc (vẫn tạo hình chữ nhật vì 2 cạnh kia có thể là dầm).
        /// </summary>
        public int AutoDrawOpeningDiagonals()
        {
            var dh = new Tekla.Structures.Drawing.DrawingHandler();
            var activeDrawing = dh.GetActiveDrawing();
            if (activeDrawing == null) return 0;

            var model = new Tekla.Structures.Model.Model();
            int openingCount = 0;

            try
            {
                // Lấy tất cả sheet/view trong drawing
                var drawingObjects = activeDrawing.GetSheet().GetAllObjects();

                while (drawingObjects.MoveNext())
                {
                    // Tìm tất cả View
                    if (!(drawingObjects.Current is Tekla.Structures.Drawing.View view))
                        continue;

                    // Lấy tất cả đối tượng trong view
                    var viewObjects = view.GetAllObjects();
                    while (viewObjects.MoveNext())
                    {
                        // Chỉ xử lý Drawing.Part
                        if (!(viewObjects.Current is Tekla.Structures.Drawing.Part drawingPart))
                            continue;

                        // Lấy Model.Part tương ứng
                        var modelObject = model.SelectModelObject(drawingPart.ModelIdentifier);
                        if (!(modelObject is Tekla.Structures.Model.Part modelPart))
                            continue;

                        // Kiểm tra boolean cuts (lỗ mở / đục)
                        var booleans = modelPart.GetBooleans();
                        if (booleans == null) continue;

                        while (booleans.MoveNext())
                        {
                            if (!(booleans.Current is Tekla.Structures.Model.BooleanPart booleanPart))
                                continue;

                            // Lấy OperativePart (cấu kiện cắt) và solid của nó
                            var operativePart = booleanPart.OperativePart;
                            if (operativePart == null) continue;

                            Tekla.Structures.Model.Solid solid = operativePart.GetSolid();
                            if (solid == null) continue;

                            // Tìm bounding box của solid (3D model coordinates)
                            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
                            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

                            var faceEnum = solid.GetFaceEnumerator();
                            bool hasPoints = false;

                            while (faceEnum.MoveNext())
                            {
                                var face = faceEnum.Current;
                                if (face == null) continue;

                                var loopEnum = face.GetLoopEnumerator();
                                while (loopEnum.MoveNext())
                                {
                                    var loop = loopEnum.Current;
                                    if (loop == null) continue;

                                    var vertexEnum = loop.GetVertexEnumerator();
                                    while (vertexEnum.MoveNext())
                                    {
                                        var vertex = vertexEnum.Current;
                                        if (vertex == null) continue;

                                        hasPoints = true;
                                        if (vertex.X < minX) minX = vertex.X;
                                        if (vertex.Y < minY) minY = vertex.Y;
                                        if (vertex.Z < minZ) minZ = vertex.Z;
                                        if (vertex.X > maxX) maxX = vertex.X;
                                        if (vertex.Y > maxY) maxY = vertex.Y;
                                        if (vertex.Z > maxZ) maxZ = vertex.Z;
                                    }
                                }
                            }

                            if (!hasPoints) continue;

                            // Tạo 8 góc bounding box 3D
                            var boxCorners3D = new Tekla.Structures.Geometry3d.Point[]
                            {
                                new Tekla.Structures.Geometry3d.Point(minX, minY, minZ),
                                new Tekla.Structures.Geometry3d.Point(maxX, minY, minZ),
                                new Tekla.Structures.Geometry3d.Point(maxX, maxY, minZ),
                                new Tekla.Structures.Geometry3d.Point(minX, maxY, minZ),
                                new Tekla.Structures.Geometry3d.Point(minX, minY, maxZ),
                                new Tekla.Structures.Geometry3d.Point(maxX, minY, maxZ),
                                new Tekla.Structures.Geometry3d.Point(maxX, maxY, maxZ),
                                new Tekla.Structures.Geometry3d.Point(minX, maxY, maxZ),
                            };

                            // Chuyển đổi sang tọa độ view 2D
                            // Lấy DisplayCoordinateSystem của view để transform
                            var viewCS = view.DisplayCoordinateSystem;
                            var matrix = Tekla.Structures.Geometry3d.MatrixFactory.ToCoordinateSystem(viewCS);

                            double vMinX = double.MaxValue, vMinY = double.MaxValue;
                            double vMaxX = double.MinValue, vMaxY = double.MinValue;

                            foreach (var corner3D in boxCorners3D)
                            {
                                var transformed = matrix.Transform(corner3D);
                                if (transformed.X < vMinX) vMinX = transformed.X;
                                if (transformed.Y < vMinY) vMinY = transformed.Y;
                                if (transformed.X > vMaxX) vMaxX = transformed.X;
                                if (transformed.Y > vMaxY) vMaxY = transformed.Y;
                            }

                            // Kiểm tra kích thước hợp lý (bỏ qua bounding box quá nhỏ)
                            double width = vMaxX - vMinX;
                            double height = vMaxY - vMinY;
                            if (width < 10 || height < 10) continue; // Bỏ qua lỗ mở quá nhỏ

                            // Tạo 4 góc hình chữ nhật trong tọa độ view
                            var c1 = new Tekla.Structures.Geometry3d.Point(vMinX, vMinY, 0);
                            var c2 = new Tekla.Structures.Geometry3d.Point(vMaxX, vMaxY, 0);
                            var c3 = new Tekla.Structures.Geometry3d.Point(vMinX, vMaxY, 0);
                            var c4 = new Tekla.Structures.Geometry3d.Point(vMaxX, vMinY, 0);

                            // Vẽ đường chéo 1: c1 -> c2
                            var line1 = new Tekla.Structures.Drawing.Line(view, c1, c2);
                            line1.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DashedLine;
                            line1.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                            line1.Insert();

                            // Vẽ đường chéo 2: c3 -> c4
                            var line2 = new Tekla.Structures.Drawing.Line(view, c3, c4);
                            line2.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DashedLine;
                            line2.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                            line2.Insert();

                            openingCount++;
                        }
                    }
                }

                if (openingCount > 0)
                {
                    activeDrawing.CommitChanges();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"AutoDrawOpeningDiagonals error: {ex.Message}");
            }

            return openingCount;
        }

        public bool DeleteObjectById(string idInput)
        {
            if (string.IsNullOrWhiteSpace(idInput) || !_teklaModel.IsConnected()) return false;

            try
            {
                Tekla.Structures.Model.Model model = _teklaModel.GetModel();
                ModelObject opt = null;

                // 1. Try numeric ID
                if (int.TryParse(idInput, out int idValue))
                {
                    opt = model.SelectModelObject(new Tekla.Structures.Identifier(idValue));
                }

                // 2. If not found, try GUID
                if (opt == null)
                {
                    try
                    {
                        if (Guid.TryParse(idInput, out Guid guidVal))
                        {
                            opt = model.SelectModelObject(new Tekla.Structures.Identifier(guidVal));
                        }
                    }
                    catch { }
                }

                if (opt != null)
                {
                    bool deleted = opt.Delete();
                    if (deleted)
                    {
                        model.CommitChanges();
                        return true;
                    }
                }
                return false;
            }
            catch { return false; }
        }

    }
}
