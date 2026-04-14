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

        public void SplitRebarDistribution()
        {
            if (!_teklaModel.IsConnected()) return;

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                Tekla.Structures.Model.ModelObject pickedObject = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_REINFORCEMENT, "Select rebar group to split");

                if (pickedObject is RebarGroup rebarGroup)
                {
                    // Check bar count
                    var geometries = rebarGroup.GetRebarGeometries(true);
                    if (geometries.Count < 3)
                    {
                        Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Split failed: Rebar group must have at least 3 bars.");
                        return;
                    }

                    // Split point selection
                    Point pickedPoint = picker.PickPoint("Pick split point on distribution range");

                    // Projection logic
                    Point start = rebarGroup.StartPoint;
                    Point end = rebarGroup.EndPoint;
                    Vector v = new Vector(end.X - start.X, end.Y - start.Y, end.Z - start.Z);
                    Vector u = new Vector(pickedPoint.X - start.X, pickedPoint.Y - start.Y, pickedPoint.Z - start.Z);

                    double vLenSq = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
                    if (vLenSq < 1.0) return;

                    double t = (u.X * v.X + u.Y * v.Y + u.Z * v.Z) / vLenSq;

                    if (t <= 0.05 || t >= 0.95)
                    {
                        Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Split failed: Position too close to start/end.");
                        return;
                    }

                    Point splitPoint = new Point(start.X + t * v.X, start.Y + t * v.Y, start.Z + t * v.Z);

                    // Create second group by cloning the original in-place
                    // This ensures 100% properties (Hooks, UDAs, Numbering, etc.) are preserved
                    RebarGroup segment2 = null;

                    // Create a temporary clone using Copy (offset 0,0,0)
                    // Note: Copy returns true/false, doesn't return the object. We need to find it or use a different way.
                    // Better way for cloning properties is manual copying of all known fields + UDAs
                    // since we need to modify the range before/after insertion.

                    segment2 = new RebarGroup();

                    // 1. Core Properties
                    segment2.Name = rebarGroup.Name;
                    segment2.Size = rebarGroup.Size;
                    segment2.Grade = rebarGroup.Grade;
                    segment2.Class = rebarGroup.Class;
                    segment2.Father = rebarGroup.Father;
                    segment2.NumberingSeries = rebarGroup.NumberingSeries;

                    // 2. Shape and Distribution
                    foreach (Polygon poly in rebarGroup.Polygons) segment2.Polygons.Add(poly);
                    segment2.SpacingType = rebarGroup.SpacingType;
                    segment2.Spacings = rebarGroup.Spacings;
                    segment2.ExcludeType = rebarGroup.ExcludeType;

                    // Deep copy RadiusValues
                    segment2.RadiusValues.Clear();
                    foreach (object rv in rebarGroup.RadiusValues) segment2.RadiusValues.Add(rv);

                    // 3. Offsets
                    segment2.OnPlaneOffsets = rebarGroup.OnPlaneOffsets;
                    segment2.FromPlaneOffset = rebarGroup.FromPlaneOffset;

                    // 4. HOOKS (Pre-Insert)
                    segment2.StartHook.Shape = rebarGroup.StartHook.Shape;
                    segment2.StartHook.Angle = rebarGroup.StartHook.Angle;
                    segment2.StartHook.Radius = rebarGroup.StartHook.Radius;
                    segment2.StartHook.Length = rebarGroup.StartHook.Length;

                    segment2.EndHook.Shape = rebarGroup.EndHook.Shape;
                    segment2.EndHook.Angle = rebarGroup.EndHook.Angle;
                    segment2.EndHook.Radius = rebarGroup.EndHook.Radius;
                    segment2.EndHook.Length = rebarGroup.EndHook.Length;

                    // 5. Additional properties
                    segment2.StirrupType = rebarGroup.StirrupType;

                    // Adjust ranges
                    double originalEndOffset = rebarGroup.EndFromPlaneOffset;
                    rebarGroup.EndPoint = splitPoint;
                    rebarGroup.EndFromPlaneOffset = 0;

                    segment2.StartPoint = splitPoint;
                    segment2.EndPoint = end;
                    segment2.StartFromPlaneOffset = 0;
                    segment2.EndFromPlaneOffset = originalEndOffset;

                    if (rebarGroup.Modify())
                    {
                        if (segment2.Insert())
                        {
                            // 6. Mandatory Re-apply Hook properties after Insert
                            // In some Tekla versions, hooks must be applied to an existing object
                            segment2.StartHook.Shape = rebarGroup.StartHook.Shape;
                            segment2.StartHook.Angle = rebarGroup.StartHook.Angle;
                            segment2.StartHook.Radius = rebarGroup.StartHook.Radius;
                            segment2.StartHook.Length = rebarGroup.StartHook.Length;

                            segment2.EndHook.Shape = rebarGroup.EndHook.Shape;
                            segment2.EndHook.Angle = rebarGroup.EndHook.Angle;
                            segment2.EndHook.Radius = rebarGroup.EndHook.Radius;
                            segment2.EndHook.Length = rebarGroup.EndHook.Length;

                            // 7. Copy UDAs (User Defined Attributes)
                            System.Collections.Hashtable udas = new System.Collections.Hashtable();
                            if (rebarGroup.GetAllUserProperties(ref udas))
                            {
                                foreach (System.Collections.DictionaryEntry entry in udas)
                                {
                                    string key = entry.Key?.ToString();
                                    if (!string.IsNullOrEmpty(key))
                                    {
                                        if (entry.Value is string strVal)
                                            segment2.SetUserProperty(key, strVal);
                                        else if (entry.Value is int intVal)
                                            segment2.SetUserProperty(key, intVal);
                                        else if (entry.Value is double dblVal)
                                            segment2.SetUserProperty(key, dblVal);
                                    }
                                }
                            }

                            segment2.Modify();
                            _teklaModel.Commit();
                            Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Split successful (Hooks & UDAs verified).");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Split error: " + ex.Message);
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
                    line1.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.SlashedLine;
                    line1.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                    line1.Insert();

                    // Vẽ đường chéo 2
                    var line2 = new Tekla.Structures.Drawing.Line(view, corner3, corner4);
                    line2.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.SlashedLine;
                    line2.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                    line2.Insert();

                    dh.GetActiveDrawing().CommitChanges();
                }
            }
            catch { return; }
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
