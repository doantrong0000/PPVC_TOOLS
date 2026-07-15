using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;
using TestTekla.Models;
using TestTekla.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using static Tekla.Structures.ModelInternal.PickerInternal;
using ModelObjectSelector = Tekla.Structures.Model.UI.ModelObjectSelector;
using Point = Tekla.Structures.Geometry3d.Point;

namespace TeklaApp.ViewModels.PageModels
{
    public class CreateRebarViewModel : BaseViewModel
    {
        private Model _model = new Model();
        private string _findSeq = "";
        private string _statusMessage = "Ready";

        public string FindSeq
        {
            get => _findSeq;
            set { _findSeq = value; OnPropertyChanged(); }
        }

        private string _findAsm = "";
        public string FindAsm
        {
            get => _findAsm;
            set { _findAsm = value; OnPropertyChanged(); }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        private bool _isShowOnly = false;
        public bool IsShowOnly
        {
            get => _isShowOnly;
            set
            {
                _isShowOnly = value;
                OnPropertyChanged();
                if (!_isShowOnly)
                {
                    try
                    {
                        Tekla.Structures.Model.Operations.Operation.RunMacro("View_RedrawAll");
                    }
                    catch { }
                }
            }
        }

        public void CloneRebarWithMultiPoints(double coverValue, double spacingTarget)
        {
            Model _model = new Model();
            if (!_model.GetConnectionStatus()) return;

            try
            {
                Picker picker = new Picker();

                // 1. Pick source rebar
                ModelObject pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_OBJECT, "Pick source rebar to clone");
                if (!(pickedObject is Reinforcement sourceRebar)) return;

                // Get rebar shape polygon
                Polygon shapePolygon = null;
                if (sourceRebar is RebarGroup rgSource && rgSource.Polygons.Count > 0)
                    shapePolygon = rgSource.Polygons[0] as Polygon;
                else if (sourceRebar is SingleRebar srSource)
                    shapePolygon = srSource.Polygon;

                if (shapePolygon == null) return;

                // 2. Pick distribution point pairs (P1-P2, P3-P4...)
                ArrayList distPointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Pick distribution point pairs: P1-P2 (gap) P3-P4... Middle mouse to finish");
                if (distPointsList.Count < 2) return;

                List<Point> distPoints = distPointsList.Cast<Point>().ToList();
                if (distPoints.Count % 2 != 0) distPoints.RemoveAt(distPoints.Count - 1); // Ensure pairs

                List<RebarGroup> createdGroups = new List<RebarGroup>();
                int segmentCount = distPoints.Count / 2;


                for (int i = 0; i < segmentCount; i++)
                {
                    Point segStart = distPoints[2 * i];
                    Point segEnd = distPoints[2 * i + 1];
                    if (Distance.PointToPoint(segStart, segEnd) < 1.0) continue;

                    RebarGroup rg = new RebarGroup();
                    rg.Father = sourceRebar.Father;
                    rg.Polygons.Add(shapePolygon);
                    rg.StartPoint = segStart;
                    rg.EndPoint = segEnd;

                    // --- FIX FOR TEKLA 2020: ENUM SPACING ---
                    // Append 'S' to EXACT_SPACINGS
                    rg.SpacingType = BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE;
                    rg.Spacings.Clear();
                    rg.Spacings.Add(spacingTarget);

                    // Copy other properties from source
                    rg.Name = sourceRebar.Name;
                    rg.Class = sourceRebar.Class;
                    rg.NumberingSeries = sourceRebar.NumberingSeries;

                    string rSize = ""; string rGrade = "";
                    sourceRebar.GetReportProperty("SIZE", ref rSize);
                    sourceRebar.GetReportProperty("GRADE", ref rGrade);
                    rg.Size = rSize;
                    rg.Grade = rGrade;
                    rg.RadiusValues = sourceRebar.RadiusValues;

                    rg.StartPointOffsetType = sourceRebar.StartPointOffsetType;
                    rg.StartPointOffsetValue = sourceRebar.StartPointOffsetValue;

                    rg.EndPointOffsetType = sourceRebar.EndPointOffsetType;
                    rg.EndPointOffsetValue = sourceRebar.EndPointOffsetValue;

                    rg.StartFromPlaneOffset = (i == 0) ? coverValue : 0;
                    rg.EndFromPlaneOffset = (i == segmentCount - 1) ? coverValue : 0;

                    rg.OnPlaneOffsets = sourceRebar.OnPlaneOffsets;

                    rg.SetUserProperty("USER_FIELD_2", spacingTarget.ToString());

                    if (rg.Insert()) createdGroups.Add(rg);
                }

                _model.CommitChanges();

                var objs = new System.Collections.ArrayList();
                foreach (var group in createdGroups)
                {
                    objs.Add(group);
                }
                new Tekla.Structures.Model.UI.ModelObjectSelector().Select(objs);
            }
            catch (Exception ex)
            {
                // Log error if needed
                Console.WriteLine(ex.Message);
            }
        }

        public void SplitRebar()
        {
            Model _model = new Model();

            // Check model connection
            if (!_model.GetConnectionStatus())
            {
                MessageBox.Show("Cannot connect to Tekla Structures model.", "Error");
                return;
            }


            // --- USE PRE-SELECTED OBJECT ---
            ModelObjectSelector selector = new ModelObjectSelector();
            ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

            if (selectedObjects.GetSize() == 0)
            {
                MessageBox.Show("Please select a rebar in the model before running the command.", "Notification");
                return;
            }

            selectedObjects.MoveNext();
            ModelObject pickedObject = selectedObjects.Current;

            if (pickedObject is RebarGroup rebar)
            {
                // 2. Get current points of the rebar
                ArrayList list = rebar.Polygons;
                if (list.Count == 0) return;

                Polygon polygon = (Polygon)list[0];
                List<Point> pointList = polygon.Points.Cast<Point>().ToList();

                // 3. Choose split point (still need Picker for the user to click and choose position)
                Picker picker = new Picker();
                Point SplitPoint = picker.PickPoint("Select a point to split the rebar");

                // --- 4. FIND THE PROJECTION ON THE NEAREST LINE SEGMENT ---
                Point bestProjPoint = null;
                double minDistance = double.MaxValue;
                int insertIndex = -1;

                for (int i = 0; i < pointList.Count - 1; i++)
                {
                    Point A = pointList[i];
                    Point B = pointList[i + 1];

                    double abX = B.X - A.X;
                    double abY = B.Y - A.Y;
                    double abZ = B.Z - A.Z;

                    double apX = SplitPoint.X - A.X;
                    double apY = SplitPoint.Y - A.Y;
                    double apZ = SplitPoint.Z - A.Z;

                    double lengthAB_sq = (abX * abX) + (abY * abY) + (abZ * abZ);
                    if (lengthAB_sq == 0) continue;

                    double t = ((apX * abX) + (apY * abY) + (apZ * abZ)) / lengthAB_sq;
                    t = Math.Max(0, Math.Min(1, t)); // Limit t to the segment [0, 1]

                    Point projPoint = new Point(
                    A.X + t * abX,
                    A.Y + t * abY,
                    A.Z + t * abZ
                    );

                    double dist = Distance.PointToPoint(SplitPoint, projPoint);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestProjPoint = projPoint;
                        insertIndex = i;
                    }
                }

                // --- 5. PERFORM SPLIT AND UPDATE MODEL ---
                if (bestProjPoint != null && insertIndex != -1)
                {
                    // Split points into 2 parts
                    List<Point> pointsPart1 = pointList.GetRange(0, insertIndex + 1);
                    pointsPart1.Add(bestProjPoint);

                    List<Point> pointsPart2 = new List<Point> { bestProjPoint };
                    pointsPart2.AddRange(pointList.GetRange(insertIndex + 1, pointList.Count - (insertIndex + 1)));

                    // Update original rebar (Part 1)
                    ArrayList arrayList1 = new ArrayList();
                    foreach (Point pt in pointsPart1) arrayList1.Add(pt);

                    Polygon newPolygon1 = new Polygon { Points = arrayList1 };
                    rebar.Polygons.Clear();
                    rebar.Polygons.Add(newPolygon1);
                    rebar.Modify();

                    // Create new rebar (Part 2)
                    RebarGroup rebar2 = RebarMethol.CopyRebar(rebar);

                    ArrayList arrayList2 = new ArrayList();
                    foreach (Point pt in pointsPart2) arrayList2.Add(pt);

                    Polygon newPolygon2 = new Polygon { Points = arrayList2 };
                    rebar2.Polygons.Clear();
                    rebar2.Polygons.Add(newPolygon2);

                    if (rebar2.Insert())
                    {
                        _model.CommitChanges();
                    }
                }
            }

        }

        public void AddPointInRebar()
        {
            Model _model = new Model();

            // Check model connection
            if (!_model.GetConnectionStatus())
            {
                MessageBox.Show("Cannot connect to Tekla Structures model.", "Error");
                return;
            }

            // --- USE PRE-SELECTED OBJECT ---
            ModelObjectSelector selector = new ModelObjectSelector();
            ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

            if (selectedObjects.GetSize() == 0)
            {
                MessageBox.Show("Please select a rebar in the model before running the command.", "Notification");
                return;
            }

            selectedObjects.MoveNext();
            ModelObject pickedObject = selectedObjects.Current;

            if (pickedObject is RebarGroup rebar)
            {
                // 2. Get current points of the rebar
                ArrayList list = rebar.Polygons;
                if (list.Count == 0) return;

                Polygon polygon = (Polygon)list[0];
                List<Point> pointList = polygon.Points.Cast<Point>().ToList();

                // 3. Choose point to add (still need Picker for the user to click and choose position)
                Picker picker = new Picker();
                Point AddPoint = picker.PickPoint("Select a position to add a point to the rebar");

                // --- 4. FIND THE PROJECTION ON THE NEAREST LINE SEGMENT ---
                Point bestProjPoint = null;
                double minDistance = double.MaxValue;
                int insertIndex = -1;

                for (int i = 0; i < pointList.Count - 1; i++)
                {
                    Point A = pointList[i];
                    Point B = pointList[i + 1];

                    double abX = B.X - A.X;
                    double abY = B.Y - A.Y;
                    double abZ = B.Z - A.Z;

                    double apX = AddPoint.X - A.X;
                    double apY = AddPoint.Y - A.Y;
                    double apZ = AddPoint.Z - A.Z;

                    double lengthAB_sq = (abX * abX) + (abY * abY) + (abZ * abZ);
                    if (lengthAB_sq == 0) continue;

                    double t = ((apX * abX) + (apY * abY) + (apZ * abZ)) / lengthAB_sq;
                    t = Math.Max(0, Math.Min(1, t)); // Limit t to the segment [0, 1]

                    Point projPoint = new Point(
                    A.X + t * abX,
                    A.Y + t * abY,
                    A.Z + t * abZ
                    );

                    double dist = Distance.PointToPoint(AddPoint, projPoint);

                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        bestProjPoint = projPoint;
                        insertIndex = i; // Save index of point A in segment AB
                    }
                }

                // --- 5. PERFORM ADD POINT AND UPDATE MODEL ---
                if (bestProjPoint != null && insertIndex != -1)
                {
                    // Insert new projected point between point A (insertIndex) and point B (insertIndex + 1)
                    pointList.Insert(insertIndex + 1, bestProjPoint);

                    // Update point array for original rebar
                    ArrayList newArrayList = new ArrayList();
                    foreach (Point pt in pointList)
                    {
                        newArrayList.Add(pt);
                    }

                    Polygon updatedPolygon = new Polygon { Points = newArrayList };
                    rebar.Polygons.Clear();
                    rebar.Polygons.Add(updatedPolygon);

                    // Modify to apply changes to the rebar
                    if (rebar.Modify())
                    {
                        _model.CommitChanges();
                    }
                }
            }

        }

        public void ReversePointRebar()
        {
            Model _model = new Model();

            // Check model connection
            if (!_model.GetConnectionStatus())
            {
                MessageBox.Show("Cannot connect to Tekla Structures model.", "Error");
                return;
            }


            // --- USE PRE-SELECTED OBJECT ---
            ModelObjectSelector selector = new ModelObjectSelector();
            ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

            if (selectedObjects.GetSize() == 0)
            {
                MessageBox.Show("Please select a rebar in the model before running the command.", "Notification");
                return;
            }

            selectedObjects.MoveNext();
            ModelObject pickedObject = selectedObjects.Current;

            if (pickedObject is RebarGroup rebar)
            {
                // 2. Get current points of the rebar
                ArrayList list = rebar.Polygons;
                if (list.Count == 0) return;

                Polygon polygon = (Polygon)list[0];
                List<Point> pointList = polygon.Points.Cast<Point>().ToList();
                pointList.Reverse();

                ArrayList newArrayList = new ArrayList();
                foreach (Point pt in pointList)
                {
                    newArrayList.Add(pt);
                }

                Polygon updatedPolygon = new Polygon { Points = newArrayList };
                rebar.Polygons.Clear();
                rebar.Polygons.Add(updatedPolygon);
                rebar.Modify();

            }
            _model.CommitChanges();

        }


        public void DeletePointRebar()
        {
            Model _model = new Model();

            // Check model connection
            if (!_model.GetConnectionStatus())
            {
                MessageBox.Show("Cannot connect to Tekla Structures model.", "Error");
                return;
            }

            try
            {
                // --- USE PRE-SELECTED OBJECT ---
                ModelObjectSelector selector = new ModelObjectSelector();
                ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

                if (selectedObjects.GetSize() == 0)
                {
                    MessageBox.Show("Please select a rebar in the model before running the command.", "Notification");
                    return;
                }

                selectedObjects.MoveNext();
                ModelObject pickedObject = selectedObjects.Current;

                if (pickedObject is RebarGroup rebar)
                {
                    // 1. Get current list of points of the rebar
                    ArrayList list = rebar.Polygons;
                    if (list.Count == 0) return;

                    Polygon polygon = (Polygon)list[0];
                    List<Point> pointList = polygon.Points.Cast<Point>().ToList();

                    // SAFETY CHECK: Do not allow deletion if the rebar has only 2 points
                    if (pointList.Count <= 2)
                    {
                        MessageBox.Show("The rebar has only 2 points. Cannot delete more points to maintain its basic shape.", "Notification");
                        return;
                    }

                    // 2. Ask the user to select a point near the position to delete
                    Picker picker = new Picker();
                    Point pickedPoint = picker.PickPoint("Click near the node you want to delete on the rebar");

                    // 3. FIND NEAREST POINT TO DELETE
                    // Sort points by distance from clicked point and take the first one (closest)
                    Point closestPoint = pointList.OrderBy(pt => Distance.PointToPoint(pt, pickedPoint)).FirstOrDefault();

                    if (closestPoint != null)
                    {
                        // Remove point from list
                        pointList.Remove(closestPoint);

                        // 4. Recreate Polygon structure and update rebar
                        ArrayList newArrayList = new ArrayList();
                        foreach (Point pt in pointList)
                        {
                            newArrayList.Add(pt);
                        }

                        Polygon updatedPolygon = new Polygon { Points = newArrayList };
                        rebar.Polygons.Clear();
                        rebar.Polygons.Add(updatedPolygon);

                        // 5. Update model
                        if (rebar.Modify())
                        {
                            _model.CommitChanges();
                        }
                        else
                        {
                            MessageBox.Show("Cannot update rebar after deleting the point.", "Error");
                        }
                    }
                }
            }
            catch
            {
                // Catch error when user presses ESC to cancel PickPoint command
            }
        }


        public void PickAssembly()
        {
            if (!_model.GetConnectionStatus()) return;
            try
            {
                Picker picker = new Picker();
                ModelObject pickedPart = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Pick a part to get its assembly/cast unit mark");
                if (pickedPart is Part part)
                {
                    string asmName = "";
                    Assembly asm = part.GetAssembly();
                    if (asm != null)
                    {
                        asmName = asm.Name;
                    }

                    FindAsm = asmName;
                    StatusMessage = $"Selected Assembly Name: {FindAsm}";
                }
            }
            catch (Exception)
            {
                StatusMessage = "Pick cancelled or failed.";
            }
        }

        public void RunFindRebar()
        {
            if (!_model.GetConnectionStatus())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            if (string.IsNullOrWhiteSpace(FindSeq))
            {
                StatusMessage = "Please enter SEQ number(s).";
                return;
            }

            List<int> targetSeqs = FindSeq.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
           .Select(s => int.TryParse(s, out int val) ? val : (int?)null)
           .Where(v => v.HasValue)
           .Select(v => v.Value)
                            .ToList();

            if (targetSeqs.Count == 0)
            {
                StatusMessage = "Invalid SEQ input.";
                return;
            }

            string targetAsmName = FindAsm?.Trim() ?? "";
            ArrayList foundRebars = new ArrayList();
            ArrayList allRebarsInScope = new ArrayList();

            try
            {
                if (!string.IsNullOrEmpty(targetAsmName))
                {
                    // Filter by Assembly
                    ModelObjectEnumerator asmEnum = _model.GetModelObjectSelector().GetAllObjectsWithType(ModelObject.ModelObjectEnum.ASSEMBLY);
                    while (asmEnum.MoveNext())
                    {
                        var assembly = asmEnum.Current as Tekla.Structures.Model.Assembly;
                        if (assembly == null) continue;

                        string currentAsmName = assembly.Name;
                        if (string.IsNullOrEmpty(currentAsmName))
                        {
                            currentAsmName = assembly.AssemblyNumber.Prefix;
                        }

                        if (currentAsmName == targetAsmName)
                        {
                            List<Part> allParts = new List<Part>();
                            if (assembly.GetMainPart() is Part mainPart) allParts.Add(mainPart);
                            ArrayList secondaryObjects = assembly.GetSecondaries();
                            foreach (var obj in secondaryObjects)
                            {
                                if (obj is Part secPart) allParts.Add(secPart);
                            }

                            foreach (Part part in allParts)
                            {
                                ModelObjectEnumerator rebarEnum = part.GetReinforcements();
                                while (rebarEnum.MoveNext())
                                {
                                    if (rebarEnum.Current is Reinforcement rebar)
                                    {
                                        allRebarsInScope.Add(rebar);
                                        if (CheckRebarSeq(rebar, targetSeqs))
                                        {
                                            foundRebars.Add(rebar);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    Type[] rebarTypes = new Type[]
                    {
                                                                                                            typeof(Tekla.Structures.Model.SingleRebar),
                                                                                                            typeof(Tekla.Structures.Model.RebarGroup)
                        // Bạn có thể thêm typeof(RebarMesh), typeof(RebarStrand) vào đây nếu dự án có dùng
                    };
                    // Scan whole Model
                    ModelObjectEnumerator rebarEnum = _model.GetModelObjectSelector().GetAllObjectsWithType(rebarTypes);
                    while (rebarEnum.MoveNext())
                    {
                        if (rebarEnum.Current is Reinforcement rebar)
                        {
                            allRebarsInScope.Add(rebar);
                            if (CheckRebarSeq(rebar, targetSeqs))
                            {
                                foundRebars.Add(rebar);
                            }
                        }
                    }
                }

                if (IsShowOnly)
                {
                    TriggerShowOnlyFound(foundRebars, allRebarsInScope);
                }
                else
                {
                    new Tekla.Structures.Model.UI.ModelObjectSelector().Select(foundRebars);
                }

                if (foundRebars.Count > 0)
                {
                    StatusMessage = $"Found {foundRebars.Count} rebar(s).";
                }
                else
                {
                    StatusMessage = "No rebar found with specified SEQ.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Search error: " + ex.Message;
            }
        }

        private bool CheckRebarSeq(Reinforcement rebar, List<int> targetSeqs)
        {
            int valInt = 0;
            string valStr = "";
            if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valInt))
            {
                return targetSeqs.Contains(valInt);
            }
            if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valStr) && int.TryParse(valStr, out int parsed))
            {
                return targetSeqs.Contains(parsed);
            }
            return false;
        }

        private void TriggerShowOnlyFound(ArrayList foundRebars, ArrayList allRebarsInScope)
        {
            string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
            string macroDir = GetMacroDirectory();
            if (string.IsNullOrEmpty(macroDir)) return;

            // 1. Redraw all to start clean (optional but recommended)
            try
            {
                string templatePath = Path.Combine(appFolder, "Macro", "RedrawView.cs");
                if (File.Exists(templatePath))
                {
                    string macroContent = File.ReadAllText(templatePath);
                    string tempMacroName = "Temp_Run_RedrawView.cs";
                    string tempRunPath = Path.Combine(macroDir, tempMacroName);
                    File.WriteAllText(tempRunPath, macroContent);
                    Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName);
                }
            }
            catch { }

            // 2. Identify items to hide
            ArrayList toHide = new ArrayList();
            var foundSet = new HashSet<Reinforcement>(foundRebars.Cast<Reinforcement>());
            foreach (var obj in allRebarsInScope)
            {
                if (obj is Reinforcement rebar && !foundSet.Contains(rebar))
                {
                    toHide.Add(rebar);
                }
            }

            // 3. Select items to hide and run Hide macro
            if (toHide.Count > 0)
            {
                new Tekla.Structures.Model.UI.ModelObjectSelector().Select(toHide);
                try
                {
                    string templatePath = Path.Combine(appFolder, "Macro", "HideElement.cs");
                    if (File.Exists(templatePath))
                    {
                        string macroContent = File.ReadAllText(templatePath);
                        string tempMacroName = "Temp_Run_HideElement.cs";
                        string tempRunPath = Path.Combine(macroDir, tempMacroName);
                        File.WriteAllText(tempRunPath, macroContent);
                        Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName);
                    }
                }
                catch { }
            }

            // 4. Restore selection to found items
            new Tekla.Structures.Model.UI.ModelObjectSelector().Select(foundRebars);
        }

        private string GetMacroDirectory()
        {
            string macroDir = string.Empty;
            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroDir);
            if (string.IsNullOrEmpty(macroDir)) return string.Empty;
            if (macroDir.Contains(";")) macroDir = macroDir.Split(';')[0];

            string drawingMacroPath = Path.Combine(macroDir, "drawings");
            if (!Directory.Exists(drawingMacroPath)) Directory.CreateDirectory(drawingMacroPath);

            return drawingMacroPath;
        }
    }
}
