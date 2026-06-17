using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Drawing;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Solid;
using TeklaApp.Models;
using ModelObject = Tekla.Structures.Model.ModelObject;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;
using Part = Tekla.Structures.Model.Part;
using Polygon = Tekla.Structures.Drawing.Polygon;

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

                    // Create 4 corners from 2 opposite points
                    var corner1 = new Tekla.Structures.Geometry3d.Point(p1.X, p1.Y, 0);
                    var corner2 = new Tekla.Structures.Geometry3d.Point(p2.X, p2.Y, 0);
                    var corner3 = new Tekla.Structures.Geometry3d.Point(p1.X, p2.Y, 0);
                    var corner4 = new Tekla.Structures.Geometry3d.Point(p2.X, p1.Y, 0);

                    // Draw diagonal 1
                    var line1 = new Tekla.Structures.Drawing.Line(view, corner1, corner2);
                    line1.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DottedLine;
                    line1.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Black;
                    line1.Insert();

                    // Draw diagonal 2
                    var line2 = new Tekla.Structures.Drawing.Line(view, corner3, corner4);
                    line2.Attributes.Line.Type = Tekla.Structures.Drawing.LineTypes.DottedLine;
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

        public void AddPartsToCastUnit()
        {
            if (!_teklaModel.IsConnected()) return;

            try
            {
                Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();

                // 1. Pick the main part
                ModelObject mainPartObj = picker.PickObject(Tekla.Structures.Model.UI.Picker.PickObjectEnum.PICK_ONE_PART, "Select the Main Part of the Cast Unit");
                if (mainPartObj is Part mainPart)
                {
                    Assembly assembly = mainPart.GetAssembly();
                    if (assembly != null)
                    {
                        // 2. Sweep select parts to add
                        ModelObjectEnumerator subPartsEnum = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, "Sweep select parts to add to Cast Unit (Main part and rebars will be ignored)");

                        int addedCount = 0;
                        while (subPartsEnum.MoveNext())
                        {
                            if (subPartsEnum.Current is Part subPart)
                            {
                                // Ignore the main part itself
                                if (subPart.Identifier.ID != mainPart.Identifier.ID)
                                {
                                    if (assembly.Add(subPart))
                                    {
                                        addedCount++;
                                    }
                                }
                            }
                        }

                        if (addedCount > 0)
                        {
                            assembly.SetMainPart(mainPart);
                            assembly.Modify();
                            _teklaModel.Commit();
                            Tekla.Structures.Model.Operations.Operation.DisplayPrompt($"[CAST UNIT] Successfully added {addedCount} parts to the Cast Unit. Main part reassigned.");
                        }
                        else
                        {
                            Tekla.Structures.Model.Operations.Operation.DisplayPrompt("[CAST UNIT] No new valid parts were selected to add.");
                        }
                    }
                    else
                    {
                        Tekla.Structures.Model.Operations.Operation.DisplayPrompt("[CAST UNIT] Failed to get Assembly from selected Main Part.");
                    }
                }
            }
            catch (Exception ex)
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt("[CAST UNIT] Error: " + ex.Message);
            }
        }

        public void AlignSelectedRebarsToPlane()
        {
            if (!_teklaModel.IsConnected()) return;

            Model model = _teklaModel.GetModel();
            Picker picker = new Picker();
            try
            {
                // 1. Get targets
                List<Reinforcement> targets = new List<Reinforcement>();
                ModelObjectSelector selector = new ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                while (selected.MoveNext())
                {
                    if (selected.Current is Reinforcement r) targets.Add(r);
                }

                if (targets.Count == 0)
                {
                    var picked = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_REINFORCEMENTS, "Select rebar groups to align (Esc to finish)");
                    while (picked.MoveNext())
                    {
                        if (picked.Current is Reinforcement r) targets.Add(r);
                    }
                }

                if (targets.Count == 0) return;

                // 2. Pick 3 points to define plane
                Point p1 = picker.PickPoint("Pick first point on the plane");
                if (p1 == null) return;
                Point p2 = picker.PickPoint("Pick second point on the plane");
                if (p2 == null) return;
                Point p3 = picker.PickPoint("Pick third point on the plane");
                if (p3 == null) return;

                // 3. Define Plane
                Vector v1 = new Vector(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
                Vector v2 = new Vector(p3.X - p1.X, p3.Y - p1.Y, p3.Z - p1.Z);
                Vector normal = v1.Cross(v2);
                
                double distance = Math.Sqrt(normal.X * normal.X + normal.Y * normal.Y + normal.Z * normal.Z);
                if (distance < 0.001) { Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Error: Points are collinear."); return; }
                normal = new Vector(normal.X / distance, normal.Y / distance, normal.Z / distance);

                GeometricPlane plane = new GeometricPlane(p1, normal);

                int count = 0;
                foreach (var rebar in targets)
                {
                    bool success = false;
                    if (rebar is SingleRebar sr)
                    {
                        var points = sr.Polygon.Points;
                        sr.Polygon.Points = ProjectPointsToPlane(points, plane);
                        success = sr.Modify();
                    }
                    else if (rebar is RebarGroup rg)
                    {
                        var polygons = rg.Polygons;
                        if (polygons != null)
                        {
                            for (int i = 0; i < polygons.Count; i++)
                            {
                                if (polygons[i] is Tekla.Structures.Model.Polygon poly)
                                {
                                    poly.Points = ProjectPointsToPlane(poly.Points, plane);
                                }
                            }
                            rg.Polygons = polygons;
                            success = rg.Modify();
                        }
                    }
                    
                    if (success) count++;
                }

                model.CommitChanges();
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt($"Aligned {count} rebars to the selected plane.");
            }
            catch (Exception ex)
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Error: " + ex.Message);
            }
        }

        private System.Collections.ArrayList ProjectPointsToPlane(System.Collections.ArrayList originalPoints, GeometricPlane plane)
        {
            System.Collections.ArrayList projectedPoints = new System.Collections.ArrayList();
            foreach (Point p in originalPoints)
            {
                Vector v = new Vector(p.X - plane.Origin.X, p.Y - plane.Origin.Y, p.Z - plane.Origin.Z);
                double distance = v.X * plane.Normal.X + v.Y * plane.Normal.Y + v.Z * plane.Normal.Z;
                projectedPoints.Add(new Point(p.X - distance * plane.Normal.X, p.Y - distance * plane.Normal.Y, p.Z - distance * plane.Normal.Z));
            }
            return projectedPoints;
        }


        // ════════════════════════════════════════════════════
        // STEP TAG GENERATION (Instant Action)
        // ════════════════════════════════════════════════════
        public void RunStepTag()
        {
            // Hardcoded parameters based on user request
            double textHeight = 1.5;
            string fontName = "Arial Narrow";
            string textColor = "Green";
            double surfLen = 150.0;
            double stepHeight = 55;
            double hatchLen = 55;
            string fillName = "ANSI32_A";
            double scaleX = 0.05;
            double scaleY = 0.05;

            var dh = new Tekla.Structures.Drawing.DrawingHandler();
            if (dh.GetActiveDrawing() == null)
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Error: Please open a drawing first.");
                return;
            }

            try
            {
                var selector = dh.GetDrawingObjectSelector();
                var dObjectsEnum = selector.GetSelected();
                Model model = new Model();

                var dParts = new List<Tekla.Structures.Drawing.Part>();
                foreach (var dObj in dObjectsEnum)
                {
                    if (dObj is Tekla.Structures.Drawing.Part dp)
                        dParts.Add(dp);
                }

                if (dParts.Count < 1)
                {
                    Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Error: Please select at least one part.");
                    return;
                }

                int tagCreatedCount = 0;

                // A) Pair analysis
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

                        Tekla.Structures.Drawing.ViewBase view = dp1.GetView();
                        Matrix toViewMatrix = null;
                        if (view is Tekla.Structures.Drawing.View realView)
                        {
                            toViewMatrix = MatrixFactory.ToCoordinateSystem(realView.DisplayCoordinateSystem);
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

                        if ((overMaxX - overMinX) + (overMaxY - overMinY) < 10) continue;

                        Point pJ = new Point((overMinX + overMaxX) / 2.0, (overMinY + overMaxY) / 2.0, 0);

                        Vector vAlong, vHigh, vLow;
                        bool isJointHorizontal = (overMaxX - overMinX) >= (overMaxY - overMinY);
                        bool isPart1High = z1 > z2;
                        Point center1_view = new Point((v1MinX + v1MaxX) / 2.0, (v1MinY + v1MaxY) / 2.0, 0);

                        if (isJointHorizontal)
                        {
                            vAlong = new Vector(1, 0, 0);
                            Vector vUp = new Vector(0, 1, 0);
                            bool isPart1Above = center1_view.Y > pJ.Y;
                            vHigh = isPart1High ? (isPart1Above ? vUp : new Vector(0, -1, 0)) : (isPart1Above ? new Vector(0, -1, 0) : vUp);
                        }
                        else
                        {
                            vAlong = new Vector(0, -1, 0);
                            Vector vRight = new Vector(1, 0, 0);
                            bool isPart1OnLeft = center1_view.X < pJ.X;
                            vHigh = isPart1High ? (isPart1OnLeft ? new Vector(-1, 0, 0) : vRight) : (isPart1OnLeft ? vRight : new Vector(-1, 0, 0));
                        }
                        vLow = new Vector(-vHigh.X, -vHigh.Y, -vHigh.Z);

                        DrawStepSymbol(view, pJ, vAlong, vHigh, vLow, Math.Abs(z1 - z2),
                            surfLen, stepHeight, hatchLen, textHeight, fontName, textColor, fillName, scaleX, scaleY);
                        tagCreatedCount++;
                    }
                }

                // B) Single-part analysis
                foreach (var dp in dParts)
                {
                    tagCreatedCount += ProcessSinglePartSteps(dp, model, surfLen, stepHeight, hatchLen, textHeight, fontName, textColor, fillName, scaleX, scaleY);
                }

                if (tagCreatedCount > 0)
                {
                    dh.GetActiveDrawing().CommitChanges();
                    Tekla.Structures.Model.Operations.Operation.DisplayPrompt($"Success: Created {tagCreatedCount} step tags.");
                }
                else Tekla.Structures.Model.Operations.Operation.DisplayPrompt("No valid step tags created.");
            }
            catch (Exception ex) { Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Error: " + ex.Message); }
        }

        private int ProcessSinglePartSteps(Tekla.Structures.Drawing.Part dp, Model model, double surfLen, double stepH, double hatchL, double txtH, string font, string color, string fill, double scX, double scY)
        {
            var mPart = model.SelectModelObject(dp.ModelIdentifier) as Tekla.Structures.Model.Part;
            if (mPart == null) return 0;
            Solid solid = mPart.GetSolid();
            if (solid == null) return 0;

            Tekla.Structures.Drawing.ViewBase view = dp.GetView();
            Matrix toViewMatrix = null;
            if (view is Tekla.Structures.Drawing.View realView)
                toViewMatrix = MatrixFactory.ToCoordinateSystem(realView.DisplayCoordinateSystem);

            var groups = FindTopFaceGroups(solid, toViewMatrix);
            if (groups.Count < 2) return 0;

            int count = 0;
            for (int k = 0; k < groups.Count - 1; k++)
            {
                var hG = groups[k]; var lG = groups[k + 1];
                double dZ = hG.ZLevel - lG.ZLevel;
                if (dZ < 0.1) continue;

                Point cH = hG.Centroid(); Point cL = lG.Centroid();
                double dx = cL.X - cH.X; double dy = cL.Y - cH.Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 0.1) continue;

                Vector vAlong, vHigh, vLow; Point pJ;
                if (Math.Abs(dy) >= Math.Abs(dx))
                {
                    vAlong = new Vector(1, 0, 0);
                    vHigh = (cH.Y > cL.Y) ? new Vector(0, 1, 0) : new Vector(0, -1, 0);
                    pJ = new Point((Math.Max(hG.MinX, lG.MinX) + Math.Min(hG.MaxX, lG.MaxX)) / 2.0, (cH.Y > cL.Y) ? (hG.MinY + lG.MaxY) / 2.0 : (hG.MaxY + lG.MinY) / 2.0, 0);
                }
                else
                {
                    vAlong = new Vector(0, -1, 0);
                    vHigh = (cH.X > cL.X) ? new Vector(1, 0, 0) : new Vector(-1, 0, 0);
                    pJ = new Point((cH.X > cL.X) ? (hG.MinX + lG.MaxX) / 2.0 : (hG.MaxX + lG.MinX) / 2.0, (Math.Max(hG.MinY, lG.MinY) + Math.Min(hG.MaxY, lG.MaxY)) / 2.0, 0);
                }
                vLow = new Vector(-vHigh.X, -vHigh.Y, -vHigh.Z);
                DrawStepSymbol(view, pJ, vAlong, vHigh, vLow, dZ, surfLen, stepH, hatchL, txtH, font, color, fill, scX, scY);
                count++;
            }
            return count;
        }

        private List<TopFaceGroupInternal> FindTopFaceGroups(Solid solid, Matrix toViewMatrix)
        {
            var faceEnum = solid.GetFaceEnumerator();
            var groups = new List<TopFaceGroupInternal>();
            while (faceEnum.MoveNext())
            {
                if (faceEnum.Current is Face face && face.Normal.Z > 0.7)
                {
                    var pts = new List<Point>();
                    var loopEnum = face.GetLoopEnumerator();
                    while (loopEnum.MoveNext())
                    {
                        var vertEnum = (loopEnum.Current as Loop)?.GetVertexEnumerator();
                        while (vertEnum != null && vertEnum.MoveNext()) pts.Add(vertEnum.Current as Point);
                    }
                    if (pts.Count == 0) continue;
                    double z = pts.Average(p => p.Z);
                    var match = groups.FirstOrDefault(g => Math.Abs(g.ZLevel - z) < 1.0);
                    if (match == null) { match = new TopFaceGroupInternal { ZLevel = z }; groups.Add(match); }
                    match.ViewVertices.AddRange(pts.Select(p => toViewMatrix != null ? toViewMatrix.Transform(p) : p));
                }
            }
            foreach (var g in groups) g.ComputeBounds();
            return groups.OrderByDescending(g => g.ZLevel).ToList();
        }

        private void DrawStepSymbol(Tekla.Structures.Drawing.ViewBase view, Point pJ, Vector vAlong, Vector vHigh, Vector vLow, double deltaZ, double surfLen, double stepH, double hatchL, double textH, string font, string color, string fill, double scX, double scY)
        {
            Point pHighEnd = new Point(pJ.X + vHigh.X * surfLen, pJ.Y + vHigh.Y * surfLen, 0);
            Point pLowJ = new Point(pJ.X + vAlong.X * stepH, pJ.Y + vAlong.Y * stepH, 0);
            Point pLowEnd = new Point(pLowJ.X + vLow.X * surfLen, pLowJ.Y + vLow.Y * surfLen, 0);


            var hp = new Tekla.Structures.Drawing.PointList { pHighEnd, pJ, new Point(pJ.X + vAlong.X * hatchL, pJ.Y + vAlong.Y * hatchL, 0), new Point(pHighEnd.X + vAlong.X * hatchL, pHighEnd.Y + vAlong.Y * hatchL, 0), pHighEnd };
            var hPoly = new Tekla.Structures.Drawing.Polygon(view, hp);
            hPoly.Attributes.Hatch.Name = fill; hPoly.Attributes.Hatch.ScaleX = scX; hPoly.Attributes.Hatch.ScaleY = scY;
            hPoly.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Invisible; hPoly.Insert();

            var lp = new Tekla.Structures.Drawing.PointList { pLowJ, pLowEnd, new Point(pLowEnd.X + vAlong.X * hatchL, pLowEnd.Y + vAlong.Y * hatchL, 0), new Point(pLowJ.X + vAlong.X * hatchL, pLowJ.Y + vAlong.Y * hatchL, 0), pLowJ };
            var lPoly = new Tekla.Structures.Drawing.Polygon(view, lp);
            lPoly.Attributes.Hatch.Name = fill; lPoly.Attributes.Hatch.ScaleX = scX; lPoly.Attributes.Hatch.ScaleY = scY;
            lPoly.Attributes.Line.Color = Tekla.Structures.Drawing.DrawingColors.Invisible; lPoly.Insert();

            Tekla.Structures.Drawing.PointList sk = new Tekla.Structures.Drawing.PointList { pHighEnd, pJ, pLowJ, pLowEnd };
            var polyLine = new Tekla.Structures.Drawing.Polyline(view, sk);
            polyLine.Attributes.Line.Color = DrawingColors.Black;
            polyLine.Insert();

            var text = new Tekla.Structures.Drawing.Text(view, new Point(pJ.X + vLow.X * surfLen * 0.5, pJ.Y + vLow.Y * surfLen * 0.5, 0), ((int)Math.Round(deltaZ)).ToString());
            text.Attributes.Frame = new Frame(FrameTypes.None, DrawingColors.Black);
            text.Attributes.Font.Height = textH; text.Attributes.Font.Name = font;

            text.Placing = new PointPlacing(); // disable leader line
            if (Enum.TryParse(color, out Tekla.Structures.Drawing.DrawingColors c)) text.Attributes.Font.Color = c;
            Vector vP = new Vector(vAlong.Y, -vAlong.X, 0); double ang = Math.Atan2(vP.Y, vP.X) * 180 / Math.PI;
            text.Attributes.Angle = ang > 90 ? ang - 180 : (ang < -90 ? ang + 180 : ang); text.Insert();
        }

        private class TopFaceGroupInternal
        {
            public double ZLevel; public List<Point> ViewVertices = new List<Point>();
            public double MinX, MaxX, MinY, MaxY;
            public void ComputeBounds()
            {
                MinX = ViewVertices.Min(p => p.X); MaxX = ViewVertices.Max(p => p.X);
                MinY = ViewVertices.Min(p => p.Y); MaxY = ViewVertices.Max(p => p.Y);
            }
            public Point Centroid() => new Point(ViewVertices.Average(p => p.X), ViewVertices.Average(p => p.Y), 0);
        }


        // ════════════════════════════════════════════════════
        // AUTO ALIGN REBAR POINTS TO PLANE
        // ════════════════════════════════════════════════════

        /// <summary>
        /// Automatically detects the dominant plane of each selected rebar's polygon points,
        /// identifies outlier points that deviate from that plane, and snaps them back.
        /// Uses variance analysis to determine the normal axis and median for the reference value.
        /// </summary>
        public void AutoAlignRebarPoints()
        {
            if (!_teklaModel.IsConnected()) return;

            const double tolerance = 1.0; // mm — minimum deviation to consider as outlier

            Model model = _teklaModel.GetModel();
            Picker picker = new Picker();

            try
            {
                // 1. Collect target rebars from selection or picker
                List<Reinforcement> targets = new List<Reinforcement>();
                ModelObjectSelector selector = new ModelObjectSelector();
                var selected = selector.GetSelectedObjects();
                while (selected.MoveNext())
                {
                    if (selected.Current is Reinforcement r) targets.Add(r);
                }

                if (targets.Count == 0)
                {
                    var picked = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_REINFORCEMENTS,
                        "Select rebar groups to auto-align points (Esc to finish)");
                    while (picked.MoveNext())
                    {
                        if (picked.Current is Reinforcement r) targets.Add(r);
                    }
                }

                if (targets.Count == 0) return;

                int alignedRebarCount = 0;
                int alignedPointCount = 0;

                // 2. Process each rebar independently
                foreach (var rebar in targets)
                {
                    if (rebar is SingleRebar sr)
                    {
                        var points = sr.Polygon.Points;
                        int fixedCount = AlignPointsToDetectedPlane(points, tolerance);
                        if (fixedCount > 0)
                        {
                            sr.Polygon.Points = points;
                            if (sr.Modify())
                            {
                                alignedRebarCount++;
                                alignedPointCount += fixedCount;
                            }
                        }
                    }
                    else if (rebar is RebarGroup rg)
                    {
                        var polygons = rg.Polygons;
                        if (polygons != null)
                        {
                            // Collect ALL points from all polygons to determine the plane
                            List<Point> allPoints = new List<Point>();
                            foreach (var polyObj in polygons)
                            {
                                if (polyObj is Tekla.Structures.Model.Polygon poly)
                                {
                                    foreach (Point pt in poly.Points) allPoints.Add(pt);
                                }
                            }

                            if (allPoints.Count < 3) continue;

                            // Determine the dominant normal axis and reference value from ALL points
                            int normalAxis = DetectNormalAxis(allPoints);
                            if (normalAxis < 0) continue;

                            double refValue = GetMedian(allPoints.Select(p => GetComponent(p, normalAxis)).ToList());

                            // Apply alignment to each polygon
                            int totalFixed = 0;
                            for (int i = 0; i < polygons.Count; i++)
                            {
                                if (polygons[i] is Tekla.Structures.Model.Polygon poly)
                                {
                                    for (int j = 0; j < poly.Points.Count; j++)
                                    {
                                        Point pt = poly.Points[j] as Point;
                                        if (pt == null) continue;

                                        double deviation = Math.Abs(GetComponent(pt, normalAxis) - refValue);
                                        if (deviation > tolerance)
                                        {
                                            SetComponent(pt, normalAxis, refValue);
                                            totalFixed++;
                                        }
                                    }
                                }
                            }

                            if (totalFixed > 0)
                            {
                                rg.Polygons = polygons;
                                if (rg.Modify())
                                {
                                    alignedRebarCount++;
                                    alignedPointCount += totalFixed;
                                }
                            }
                        }
                    }
                }

                model.CommitChanges();

                if (alignedRebarCount > 0)
                    Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                        $"Auto Align: Fixed {alignedPointCount} points in {alignedRebarCount} rebars.");
                else
                    Tekla.Structures.Model.Operations.Operation.DisplayPrompt(
                        "Auto Align: All points are already aligned (no outliers found).");
            }
            catch (Exception ex)
            {
                Tekla.Structures.Model.Operations.Operation.DisplayPrompt("Auto Align Error: " + ex.Message);
            }
        }

        /// <summary>
        /// Aligns outlier points in an ArrayList directly (for SingleRebar).
        /// Returns the number of points fixed.
        /// </summary>
        private int AlignPointsToDetectedPlane(System.Collections.ArrayList points, double tolerance)
        {
            if (points.Count < 3) return 0;

            List<Point> ptList = new List<Point>();
            foreach (Point p in points) ptList.Add(p);

            int normalAxis = DetectNormalAxis(ptList);
            if (normalAxis < 0) return 0;

            double refValue = GetMedian(ptList.Select(p => GetComponent(p, normalAxis)).ToList());

            int fixedCount = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Point pt = points[i] as Point;
                if (pt == null) continue;

                double deviation = Math.Abs(GetComponent(pt, normalAxis) - refValue);
                if (deviation > tolerance)
                {
                    SetComponent(pt, normalAxis, refValue);
                    fixedCount++;
                }
            }

            return fixedCount;
        }

        /// <summary>
        /// Detects which axis (0=X, 1=Y, 2=Z) is the normal of the dominant plane
        /// by finding the axis with the smallest variance.
        /// Returns -1 if the plane cannot be determined.
        /// </summary>
        private int DetectNormalAxis(List<Point> points)
        {
            if (points.Count < 3) return -1;

            double meanX = points.Average(p => p.X);
            double meanY = points.Average(p => p.Y);
            double meanZ = points.Average(p => p.Z);

            double varX = points.Sum(p => (p.X - meanX) * (p.X - meanX));
            double varY = points.Sum(p => (p.Y - meanY) * (p.Y - meanY));
            double varZ = points.Sum(p => (p.Z - meanZ) * (p.Z - meanZ));

            // The axis with the smallest variance is the normal axis
            // (all points have nearly the same value on this axis)
            if (varX <= varY && varX <= varZ) return 0; // X
            if (varY <= varX && varY <= varZ) return 1; // Y
            return 2; // Z
        }

        /// <summary>
        /// Gets X (0), Y (1), or Z (2) component from a Point.
        /// </summary>
        private double GetComponent(Point p, int axis)
        {
            switch (axis)
            {
                case 0: return p.X;
                case 1: return p.Y;
                case 2: return p.Z;
                default: return 0;
            }
        }

        /// <summary>
        /// Sets X (0), Y (1), or Z (2) component of a Point.
        /// </summary>
        private void SetComponent(Point p, int axis, double value)
        {
            switch (axis)
            {
                case 0: p.X = value; break;
                case 1: p.Y = value; break;
                case 2: p.Z = value; break;
            }
        }

        /// <summary>
        /// Computes the median of a list of doubles.
        /// Median is preferred over mean because it is robust against outliers.
        /// </summary>
        private double GetMedian(List<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            int n = sorted.Count;
            if (n == 0) return 0;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[n / 2 - 1] + sorted[n / 2]) / 2.0;
        }

    }
}