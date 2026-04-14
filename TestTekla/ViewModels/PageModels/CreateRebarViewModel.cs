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

                // 1. Chọn thép mẫu (Source)
                ModelObject pickedObject = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_OBJECT, "Chọn thép mẫu để nhân bản");
                if (!(pickedObject is Reinforcement sourceRebar)) return;

                // Lấy Polygon hình dạng thép
                Polygon shapePolygon = null;
                if (sourceRebar is RebarGroup rgSource && rgSource.Polygons.Count > 0)
                    shapePolygon = rgSource.Polygons[0] as Polygon;
                else if (sourceRebar is SingleRebar srSource)
                    shapePolygon = srSource.Polygon;

                if (shapePolygon == null) return;

                // 2. Chọn các cặp điểm rải (P1-P2, P3-P4...)
                ArrayList distPointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Chọn các cặp điểm rải: P1-P2 (Nghỉ) P3-P4... Chuột giữa để kết thúc");
                if (distPointsList.Count < 2) return;

                List<Point> distPoints = distPointsList.Cast<Point>().ToList();
                if (distPoints.Count % 2 != 0) distPoints.RemoveAt(distPoints.Count - 1); // Đảm bảo đi theo cặp

                List<RebarGroup> createdGroups = new List<RebarGroup>();
                int segmentCount = distPoints.Count / 2;
                double fixedSpacing = 400.0; // Khoảng rải mục tiêu
                double coverValue = 30.0;    // Lớp bảo vệ 2 đầu mút

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
                    // Sử dụng StartOffset và EndOffset để lùi thép 30mm
                    rg.StartFromPlaneOffset = (i == 0) ? coverValue : 0;
                    rg.EndFromPlaneOffset = (i == segmentCount - 1) ? coverValue : 0;

                    // --- FIX CHO TEKLA 2020: ENUM SPACING ---
                    // Thêm chữ S vào cuối EXACT_SPACINGS
                    rg.SpacingType = BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_TARGET_SPACE;
                    rg.Spacings.Clear();
                    rg.Spacings.Add(fixedSpacing);

                    // Copy các thuộc tính khác như cũ...
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

                // 3. Gộp nhóm và xử lý UDA
                if (mergeGroups && createdGroups.Count > 1)
                {
                    RebarGroup combinedGroup = createdGroups[0];
                    for (int j = 1; j < createdGroups.Count; j++)
                    {
                        var result = Operation.Combine(combinedGroup, createdGroups[j]);
                        if (result != null) combinedGroup = result;
                    }

                    // Gán giá trị 400 vào UDA để hiển thị trên bản vẽ (tránh số lẻ do Tekla tính toán lại)
                    // Bạn có thể đổi USER_FIELD_2 thành UDA bất kỳ bạn dùng trong Mark
                    combinedGroup.SetUserProperty("USER_FIELD_2", fixedSpacing.ToString());

                    combinedGroup.Modify();
                    _model.CommitChanges();

                    // Chọn đối tượng cuối cùng sau khi gộp
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
                // Ghi log lỗi nếu cần
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
