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

        public void RunFindRebar()
        {
            if (!_model.GetConnectionStatus())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            if (string.IsNullOrWhiteSpace(FindSeq))
            {
                StatusMessage = "Please enter a SEQ number.";
                return;
            }

            if (!int.TryParse(FindSeq.Trim(), out int targetSeq))
            {
                StatusMessage = "Invalid SEQ number. Please enter an integer.";
                return;
            }

            StatusMessage = $"Searching for SEQ {targetSeq}...";

            try
            {
                Model myModel = new Model();


                // 1. Get all reinforcement objects from the model
                ModelObjectEnumerator enumerator = myModel.GetModelObjectSelector().GetAllObjectsWithType(ModelObject.ModelObjectEnum.REBARGROUP);
                ArrayList foundObjects = new ArrayList();
                ModelObjectEnumerator enumerator2 = myModel.GetModelObjectSelector().GetAllObjectsWithType(ModelObject.ModelObjectEnum.SINGLEREBAR);

                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Reinforcement rebar)
                    {
                        bool isMatch = false;

                        // Try Method 1: Integer UDA
                        int valInt = 0;
                        if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valInt) && valInt == targetSeq)
                        {
                            isMatch = true;
                        }

                        // Try Method 2: String UDA fallback (common in custom configurations)
                        if (!isMatch)
                        {
                            string valStr = "";
                            if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valStr) &&
                            int.TryParse(valStr, out int parsedStr) &&
                            parsedStr == targetSeq)
                            {
                                isMatch = true;
                            }
                        }

                        if (isMatch)
                        {
                            foundObjects.Add(rebar);
                        }
                    }
                }

                while (enumerator2.MoveNext())
                {
                    if (enumerator2.Current is Reinforcement rebar)
                    {
                        bool isMatch = false;

                        // Try Method 1: Integer UDA
                        int valInt = 0;
                        if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valInt) && valInt == targetSeq)
                        {
                            isMatch = true;
                        }

                        // Try Method 2: String UDA fallback (common in custom configurations)
                        if (!isMatch)
                        {
                            string valStr = "";
                            if (rebar.GetUserProperty("REBAR_SEQ_NO", ref valStr) &&
                            int.TryParse(valStr, out int parsedStr) &&
                            parsedStr == targetSeq)
                            {
                                isMatch = true;
                            }
                        }

                        if (isMatch)
                        {
                            foundObjects.Add(rebar);
                        }
                    }
                }

                if (foundObjects.Count > 0)
                {
                    new Tekla.Structures.Model.UI.ModelObjectSelector().Select(foundObjects);
                    StatusMessage = $"Found and selected {foundObjects.Count} rebar(s) with SEQ {targetSeq}.";
                }
                else
                {
                    StatusMessage = $"No rebar found with SEQ {targetSeq}.";
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
