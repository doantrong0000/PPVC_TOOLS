using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.Operations;
using Tekla.Structures.Model.UI;

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeklaApp.ViewModels.PageModels
{
    public class CreateRebarViewModel : INotifyPropertyChanged
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

        public void CloneRebarWithMultiPoints(bool mergeGroups = true)
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
                double fixedSpacing = 400.0; // Target spacing
                double coverValue = 30.0;    // End cover offset

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

                    // --- FIX CHO TEKLA 2020: OFFSET ---
                    // Use StartOffset and EndOffset to offset rebar 30mm
                    rg.StartFromPlaneOffset = (i == 0) ? coverValue : 0;
                    rg.EndFromPlaneOffset = (i == segmentCount - 1) ? coverValue : 0;

                    // --- FIX CHO TEKLA 2020: ENUM SPACING ---
                    // Append 'S' to EXACT_SPACINGS
                    rg.SpacingType = BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE;
                    rg.Spacings.Clear();
                    rg.Spacings.Add(fixedSpacing);

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

                    if (rg.Insert()) createdGroups.Add(rg);
                }

                _model.CommitChanges();

                // 3. Merge groups and handle UDA
                if (mergeGroups && createdGroups.Count > 1)
                {
                    RebarGroup combinedGroup = createdGroups[0];
                    for (int j = 1; j < createdGroups.Count; j++)
                    {
                        var result = Operation.Combine(combinedGroup, createdGroups[j]);
                        if (result != null) combinedGroup = result;
                    }

                    // Set spacing value to UDA for drawing display (avoid rounding from Tekla recalculation)
                    // You can change USER_FIELD_2 to any UDA used in your Mark
                    combinedGroup.SetUserProperty("USER_FIELD_2", fixedSpacing.ToString());

                    combinedGroup.Modify();
                    _model.CommitChanges();

                    // Select the final merged object
                    ArrayList selectList = new ArrayList { combinedGroup };
                    new Tekla.Structures.Model.UI.ModelObjectSelector().Select(selectList);
                }
                else if (createdGroups.Count > 0)
                {
                    new Tekla.Structures.Model.UI.ModelObjectSelector().Select(new ArrayList(createdGroups));
                }
            }
            catch (Exception ex)
            {
                // Log error if needed
                Console.WriteLine(ex.Message);
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

            // Support multiple SEQs separated by space (e.g. "1 6")
            List<int> targetSeqs = FindSeq.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                                         .Select(s => int.TryParse(s, out int val) ? val : (int?)null)
                                         .Where(v => v.HasValue)
                                         .Select(v => v.Value)
                                         .ToList();

            if (targetSeqs.Count == 0)
            {
                StatusMessage = "Invalid SEQ input. Use integers separated by space.";
                return;
            }

            string targetAsmName = FindAsm?.Trim() ?? "";
            string seqsDisplay = string.Join(", ", targetSeqs);
            StatusMessage = string.IsNullOrEmpty(targetAsmName) ? $"Searching for SEQs: {seqsDisplay}..." : $"Searching for SEQs: {seqsDisplay} in Assembly: {targetAsmName}...";

            try
            {
                Model myModel = new Model();
                ArrayList foundObjects = new ArrayList();

                // 1. Get all reinforcement objects (RebarGroup and SingleRebar separately)
                ModelObject.ModelObjectEnum[] typesToSearch = { ModelObject.ModelObjectEnum.REBARGROUP, ModelObject.ModelObjectEnum.SINGLEREBAR };

                foreach (var type in typesToSearch)
                {
                    ModelObjectEnumerator enumerator = myModel.GetModelObjectSelector().GetAllObjectsWithType(type);
                    while (enumerator.MoveNext())
                    {
                        if (enumerator.Current is Reinforcement rebar)
                        {
                            // Assembly Filter (by Name)
                            if (!string.IsNullOrEmpty(targetAsmName))
                            {
                                string rebarAsmName = "";
                                rebar.GetReportProperty("ASSEMBLY.NAME", ref rebarAsmName);
                                if (string.IsNullOrEmpty(rebarAsmName))
                                {
                                    rebar.GetReportProperty("CAST_UNIT.NAME", ref rebarAsmName);
                                }
                                if (string.IsNullOrEmpty(rebarAsmName))
                                {
                                    rebar.GetReportProperty("NAME", ref rebarAsmName);
                                }

                                if (rebarAsmName != targetAsmName)
                                    continue;
                            }

                            // SEQ Check (support multiple)
                            int valInt = 0;
                            bool isMatch = false;

                            if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valInt))
                            {
                                if (targetSeqs.Contains(valInt)) isMatch = true;
                            }

                            if (!isMatch)
                            {
                                string valStr = "";
                                if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valStr) && int.TryParse(valStr, out int parsed))
                                {
                                    if (targetSeqs.Contains(parsed)) isMatch = true;
                                }
                            }

                            if (isMatch)
                            {
                                foundObjects.Add(rebar);
                            }
                        }
                    }
                }

                if (foundObjects.Count > 0)
                {
                    new Tekla.Structures.Model.UI.ModelObjectSelector().Select(foundObjects);
                    StatusMessage = $"Found and selected {foundObjects.Count} rebar(s) matching SEQs: {seqsDisplay}.";
                }
                else
                {
                    StatusMessage = $"No rebar found matching SEQs: {seqsDisplay}.";
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Search error: " + ex.Message;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
