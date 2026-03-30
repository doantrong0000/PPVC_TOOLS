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

        public void CreateRebarWithMultiPoints(
            double targetSpace,
            double startOffset,
            double endOffset,
            double onPlaneOffset,
            string rebarName,
            string rebarSize,
            string rebarGrade,
            int rebarClass,
            bool mergeGroups = true
        )
        {

            if (!_model.GetConnectionStatus())
            {
                return;
            }

            if (targetSpace <= 0)
            {
                return;
            }

            try
            {
                Picker picker = new Picker();

                // 1. Pick Part
                Part pickedPart = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Pick a part to reinforce") as Part;
                if (pickedPart == null) return;

                // 2. Pick Shape
                ArrayList shapePointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Pick shape points (Middle mouse button to finish)");
                if (shapePointsList.Count < 2)
                {
                    return;
                }

                // 3. Pick Distribution
                ArrayList distPointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Pick multiple distribution points for segments: P1-P2 (Gap) P3-P4 ... (Middle mouse)");
                if (distPointsList.Count < 2)
                {
                    return;
                }

                List<Point> distPoints = new List<Point>();
                foreach (Point pt in distPointsList) distPoints.Add(pt);

                // If odd number, omit the last
                if (distPoints.Count % 2 != 0)
                {
                    distPoints.RemoveAt(distPoints.Count - 1);
                }

                if (distPoints.Count < 2)
                {
                    return;
                }

                Polygon shapePolygon = new Polygon();
                foreach (Point pt in shapePointsList)
                {
                    shapePolygon.Points.Add(pt);
                }

                List<ModelObject> createdGroups = new List<ModelObject>();
                ArrayList objectsToSelect = new ArrayList();

                int segmentCount = distPoints.Count / 2;

                for (int i = 0; i < segmentCount; i++)
                {
                    Point segStart = distPoints[2 * i];
                    Point segEnd = distPoints[2 * i + 1];

                    double segLength = Distance.PointToPoint(segStart, segEnd);
                    if (segLength < 1.0) continue; // skip tiny segments

                    RebarGroup rg = new RebarGroup();
                    rg.Father = pickedPart;
                    rg.Polygons.Add(shapePolygon);

                    // Assign original pick points for this specific rebar group
                    rg.StartPoint = segStart;
                    rg.EndPoint = segEnd;

                    // Apply range offsets using FromPlane properties (as requested)
                    // First segment gets startOffset, Last segment gets endOffset
                    if (i == 0)
                    {
                        rg.StartFromPlaneOffset = startOffset;
                    }
                    if (i == segmentCount - 1)
                    {
                        rg.EndFromPlaneOffset = endOffset;
                    }

                    // On-plane offset (applies to all segments)
                    rg.OnPlaneOffsets.Clear();
                    rg.OnPlaneOffsets.Add(onPlaneOffset);

                    // Rebar Properties from UI
                    rg.Name = string.IsNullOrWhiteSpace(rebarName) ? "REBAR" : rebarName;
                    rg.Size = string.IsNullOrWhiteSpace(rebarSize) ? "10" : rebarSize;
                    rg.Grade = string.IsNullOrWhiteSpace(rebarGrade) ? "H" : rebarGrade;
                    rg.Class = rebarClass;
                    rg.RadiusValues.Add(20.0); // Default rounding radius

                    // Distribution logic
                    rg.SpacingType = BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE;
                    rg.Spacings.Clear();
                    rg.Spacings.Add(targetSpace);

                    if (rg.Insert())
                    {
                        createdGroups.Add(rg);
                    }
                }

                _model.CommitChanges();
                // 4. Merge groups if requested
                if (mergeGroups && createdGroups.Count > 1)
                {
                    RebarGroup combinedGroup = createdGroups[0] as RebarGroup;

                    for (int j = 1; j < createdGroups.Count; j++)
                    {
                        var result = Operation.Combine(combinedGroup, createdGroups[j] as RebarGroup);
                        if (result != null)
                        {
                            combinedGroup = result;
                        }
                    }

                    _model.CommitChanges();
                    objectsToSelect.Add(combinedGroup);
                }
                else
                {
                    foreach (var g in createdGroups) objectsToSelect.Add(g);
                }

                // Select the created groups in Tekla UI
                if (objectsToSelect.Count > 0)
                {
                    StatusMessage = $"Created and merged {createdGroups.Count} rebar groups.";
                    Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    selector.Select(objectsToSelect);
                }
            }
            catch (Exception ex)
            {
                StatusMessage = "Error creating rebar: " + ex.Message;
                return;
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


                // 1. Lấy toàn bộ đối tượng thép (Reinforcement) trong mô hình
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
