using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Helpers;
using TeklaApp.Models;
using TestTekla.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TS = Tekla.Structures;
using TSM = Tekla.Structures.Model;

namespace TeklaApp.ViewModels
{
    public class RebarRSQNViewModel : BaseViewModel
    {
        private readonly Model _model = new Model();
        public ObservableCollection<RSQNGroupItem> Groups { get; set; } = new ObservableCollection<RSQNGroupItem>();
        private string _statusMessage = "Ready";
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }


        public RebarRSQNViewModel()
        {
        }

        public void GetRebarsFromModel()
        {
            if (!_model.GetConnectionStatus())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            try
            {
                Groups.Clear();
                var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var selectedObjects = selector.GetSelectedObjects();
                var rebars = new List<Reinforcement>();

                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is Reinforcement r) rebars.Add(r);
                    else if (selectedObjects.Current is Part p)
                    {
                        var children = p.GetChildren();
                        while (children.MoveNext())
                        {
                            if (children.Current is Reinforcement childRebar) rebars.Add(childRebar);
                        }
                    }
                }

                if (rebars.Count == 0)
                {
                    StatusMessage = "Picking objects...";
                    var picker = new Picker();
                    var pickedEnum = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS, "Select rebars or parts (Esc to finish)");
                    while (pickedEnum.MoveNext())
                    {
                        if (pickedEnum.Current is Reinforcement r) rebars.Add(r);
                        else if (pickedEnum.Current is Part p)
                        {
                            var children = p.GetChildren();
                            while (children.MoveNext())
                            {
                                if (children.Current is Reinforcement childRebar) rebars.Add(childRebar);
                            }
                        }
                    }
                }

                if (rebars.Count == 0)
                {
                    StatusMessage = "No rebars selected.";
                    return;
                }

                // Grouping logic (re-implementing Lib.GrouprebarToCompare)
                var groupedData = GroupRebars(rebars);
                foreach (var g in groupedData.OrderBy(x => x.Seq == 0 ? 0 : 1).ThenBy(x => x.Seq))
                {
                    Groups.Add(g);
                }

                // CheckForDuplicates(); // Removed as per request to separate into buttons
                StatusMessage = $"Loaded {rebars.Count} rebars into {Groups.Count} groups.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        private List<RSQNGroupItem> GroupRebars(List<Reinforcement> rebars)
        {
            var result = new List<RSQNGroupItem>();
            // Using a combination of properties for grouping
            var grouped = rebars.GroupBy(r => GetRebarSignature(r));

            foreach (var group in grouped)
            {
                var first = group.First();
                string gName = first.Name;
                string gSize = ""; first.GetReportProperty("SIZE", ref gSize);
                string gGrade = ""; first.GetReportProperty("GRADE", ref gGrade);
                string gShape = ""; first.GetReportProperty("SHAPE", ref gShape);
                string rebarPos = ""; first.GetReportProperty("REBAR_POS", ref rebarPos);
                double weight = 0; first.GetReportProperty("WEIGHT", ref weight);
                double length = 0; first.GetReportProperty("LENGTH", ref length);
                int seq = 0; first.GetUserProperty("REBAR_SEQ_NO", ref seq);
                int qty = 0;
                foreach (var r in group)
                {
                    int n = 0; r.GetReportProperty("NUMBER", ref n);
                    qty += n;
                }

                var item = new RSQNGroupItem
                {
                    Name = gName,
                    Mark = rebarPos,
                    Grade = gGrade,
                    Size = gSize,
                    Shape = gShape,
                    Identifiers = group.Select(r => r.Identifier).ToList(),
                    BackingRebars = group.ToList(),
                    Weight = weight,
                    Length = length,
                    Quantity = qty,
                    Seq = seq
                };


                result.Add(item);
            }
            return result;
        }

        private string GetRebarSignature(Reinforcement rebar)
        {
            string size = ""; rebar.GetReportProperty("SIZE", ref size);
            string grade = ""; rebar.GetReportProperty("GRADE", ref grade);
            string rebarPos = ""; rebar.GetReportProperty("REBAR_POS", ref rebarPos);

            // Simplified geometry signature for porting
            string shapeKey = ""; rebar.GetReportProperty("SHAPE", ref shapeKey);

            double length = 0; rebar.GetReportProperty("LENGTH", ref length);
            double roundedLength = Math.Round(length / 5.0) * 5.0;
            int seq = 0; rebar.GetUserProperty("REBAR_SEQ_NO", ref seq);



            return $"{seq}|{rebarPos}|{grade}|{size}|{shapeKey}|{roundedLength}";
        }

        public void CheckRebar()
        {
            int found = 0;
            foreach (var rebar in Groups)
            {
                if (rebar.Seq == 0)
                {
                    rebar.Overlap = "Unassigned";
                    rebar.Checkagain = "Unassigned";
                }
            }
            foreach (var rebar in Groups)
            {
                if (rebar.Overlap.Contains("Overlap") || rebar.Checkagain.Contains("Unassigned")) continue; // Skip already flagged items
                var list = new List<RSQNGroupItem>();
                foreach (var otherRebar in Groups)
                {

                    if (rebar == otherRebar) continue;
                    if (otherRebar.Length == rebar.Length) list.Add(otherRebar);
                }
                bool banThemCoBiOverlapKhong = false;
                foreach (var item in list)
                {
                    if (item.Seq != rebar.Seq &&
                    item.Shape == rebar.Shape &&
                    item.Length == rebar.Length &&
                    item.Grade == rebar.Grade &&
                    Math.Round(item.Weight, 3) == Math.Round(rebar.Weight, 3))
                    {
                        item.Overlap = "Overlap";
                        banThemCoBiOverlapKhong = true; // Đánh dấu là đã phát hiện lỗi
                    }
                }
                if (banThemCoBiOverlapKhong)
                {
                    rebar.Overlap = "Overlap";
                }
            }
            foreach (var g in Groups)
            {
                if (g.Checkagain.Contains("Check Again") || g.Checkagain.Contains("Unassigned")) continue; // Skip already flagged items
                var list = new List<RSQNGroupItem>();
                foreach (var otherRebar in Groups)
                {

                    if (g == otherRebar) continue;
                    if (otherRebar.Seq == g.Seq) list.Add(otherRebar);
                }
                bool check = false;
                foreach (var item in list)
                {
                    if (item.Mark != g.Mark)
                    {
                        item.Checkagain = "Check again";
                        check = true;
                        continue;
                    }
                    if (item.Shape != g.Shape)
                    {
                        item.Checkagain = "Check again";
                        check = true;
                        continue;
                    }
                    if (item.Length != g.Length)
                    {
                        item.Checkagain = "Check again";
                        check = true;
                        continue;
                    }
                    if (item.Grade != g.Grade)
                    {
                        item.Checkagain = "Check again";
                        check = true;
                        continue;
                    }
                    if (Math.Round(item.Weight, 3) != Math.Round(g.Weight, 3))
                    {
                        item.Checkagain = "Check again";
                        check = true;
                        continue;
                    }
                }
                if (check)
                {
                    g.Checkagain = "Check again";
                }
            }

            var checkAgainItems = Groups.Where(g => g.Overlap == "Overlap")
            .OrderBy(g => g.Length);
            var normalItems = Groups.Where(g => g.Overlap != "Overlap")
            .OrderBy(g => g.Seq);
            var sorted = checkAgainItems.Concat(normalItems).ToList();

            Groups.Clear();
            foreach (var item in sorted) Groups.Add(item);

            UpdateRowColors();
        }

        public void RunFindRebar(string FindSeq)
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

            ArrayList foundRebars = new ArrayList();
            ArrayList allRebarsInScope = new ArrayList();

            try
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


                TriggerShowOnlyFound(foundRebars, allRebarsInScope);


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

        private void UpdateRowColors()
        {
            foreach (var g in Groups)
            {
                if (g.Checkagain == "Check again") g.ColorBrush = "#FFF2CC"; // Light yellow

                if (g.Overlap == "Overlap") g.ColorBrush = "#FFF3E0";

                if (g.Checkagain == "Check again" && g.Overlap == "Overlap") g.ColorBrush = "#F4B183"; // Orange

                if (g.Checkagain == "Unassigned" || g.Overlap == "Unassigned") g.ColorBrush = "#D9D9D9"; // Gray
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

        public void SelectInModel(List<RSQNGroupItem> selectedItems)
        {
            if (selectedItems == null || selectedItems.Count == 0) return;

            var objs = new System.Collections.ArrayList();
            foreach (var item in selectedItems)
            {
                foreach (var rebar in item.BackingRebars) objs.Add(rebar);
            }

            var selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
            selector.Select(objs);
            _model.CommitChanges();
        }
    }

    public class RSQNGroupItem : INotifyPropertyChanged
    {
        private string _name;
        private string _mark;
        private string _grade;
        private string _size;
        private string _shape;
        private int _quantity;
        private double _length;
        private double _weight;

        private double _seq;
        private string _checkagain = "";
        private string _overlap = "";
        private bool _isSelected;
        private string _colorBrush = "Transparent";

        // Cập nhật lại các thuộc tính Full Property có OnPropertyChanged()
        public string Name { get => _name; set { _name = value; OnPropertyChanged(); } }
        public string Mark { get => _mark; set { _mark = value; OnPropertyChanged(); } }
        public string Grade { get => _grade; set { _grade = value; OnPropertyChanged(); } }
        public string Size { get => _size; set { _size = value; OnPropertyChanged(); } }
        public string Shape { get => _shape; set { _shape = value; OnPropertyChanged(); } }

        public int Quantity { get => _quantity; set { _quantity = value; OnPropertyChanged(); } }
        public double Length { get => _length; set { _length = value; OnPropertyChanged(); } }
        public double Weight { get => _weight; set { _weight = value; OnPropertyChanged(); } }

        public double Seq
        {
            get => _seq;
            set
            {
                _seq = value;
                OnPropertyChanged();
            }
        }
        public string Checkagain
        {
            get => _checkagain;
            set
            {
                _checkagain = value;
                OnPropertyChanged();
            }
        }

        public string Overlap
        {
            get => _overlap;
            set
            {
                _overlap = value;
                OnPropertyChanged();
            }
        }
        public List<TS.Identifier> Identifiers { get; set; } = new List<TS.Identifier>();
        public List<Reinforcement> BackingRebars { get; set; } = new List<Reinforcement>();
        public List<double> ExistingSequences { get; set; } = new List<double>();

        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string ColorBrush { get => _colorBrush; set { _colorBrush = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
