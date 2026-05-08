using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Helpers;
using TeklaApp.Models;
using TSM = Tekla.Structures.Model;
using TS = Tekla.Structures;

namespace TeklaApp.ViewModels
{
    public class RebarRSQNViewModel : INotifyPropertyChanged
    {
        private readonly Model _model = new Model();
        public ObservableCollection<RSQNGroupItem> Groups { get; set; } = new ObservableCollection<RSQNGroupItem>();

        private bool _compareToOld = true;
        private bool _renumberAll;
        private bool _renumberSelected;
        private string _startNumberStr = "1";
        private string _statusMessage = "Ready";

        public bool CompareToOld { get => _compareToOld; set { _compareToOld = value; OnPropertyChanged(); } }
        public bool RenumberAll { get => _renumberAll; set { _renumberAll = value; OnPropertyChanged(); } }
        public bool RenumberSelected { get => _renumberSelected; set { _renumberSelected = value; OnPropertyChanged(); } }
        public string StartNumberStr { get => _startNumberStr; set { _startNumberStr = value; OnPropertyChanged(); } }
        public string StatusMessage { get => _statusMessage; set { _statusMessage = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

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
                foreach (var g in groupedData.OrderBy(x => x.Seq))
                {
                    Groups.Add(g);
                }

                CheckForDuplicates();
                UpdateRowColors();
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

                var item = new RSQNGroupItem
                {
                    Name = gName,
                    Mark = first.NumberingSeries?.Prefix ?? "",
                    Grade = gGrade,
                    Size = gSize,
                    Identifiers = group.Select(r => r.Identifier).ToList(),
                    BackingRebars = group.ToList()
                };

                // Get properties from Tekla
                double weight = 0; first.GetReportProperty("WEIGHT", ref weight);
                double length = 0; first.GetReportProperty("LENGTH", ref length);
                int qty = 0;
                foreach (var r in group)
                {
                    int n = 0; r.GetReportProperty("NUMBER", ref n);
                    qty += n;
                }
                item.Weight = weight;
                item.Length = length;
                item.Quantity = qty;

                // Get existing sequences
                var sequences = new List<double>();
                foreach (var r in group)
                {
                    int sInt = 0;
                    if (r.GetUserProperty("REBAR_SEQ_NO", ref sInt)) sequences.Add(sInt);
                    else
                    {
                        double sDouble = 0;
                        if (r.GetUserProperty("REBAR_SEQ_NO", ref sDouble)) sequences.Add(sDouble);
                    }
                }
                item.ExistingSequences = sequences.Distinct().ToList();
                
                if (item.ExistingSequences.Count == 1)
                {
                    item.Seq = item.ExistingSequences[0];
                }
                else if (item.ExistingSequences.Count > 1)
                {
                    item.Seq = 0.001; // Marker for overlap as in original code
                    item.Note = "Overlap";
                }
                else
                {
                    item.Seq = 0;
                    item.Note = "Unassigned";
                }

                result.Add(item);
            }
            return result;
        }

        private string GetRebarSignature(Reinforcement rebar)
        {
            string size = ""; rebar.GetReportProperty("SIZE", ref size);
            string grade = ""; rebar.GetReportProperty("GRADE", ref grade);
            string name = rebar.Name;
            string prefix = rebar.NumberingSeries?.Prefix ?? "";
            
            // Simplified geometry signature for porting
            string shapeKey = "";
            rebar.GetReportProperty("SHAPE", ref shapeKey);
            
            double length = 0;
            rebar.GetReportProperty("LENGTH", ref length);
            double roundedLength = Math.Round(length / 5.0) * 5.0;

            return $"{name}|{prefix}|{grade}|{size}|{shapeKey}|{roundedLength}";
        }

        public void ExecuteChange()
        {
            if (Groups.Count == 0) return;

            if (CompareToOld) CompareToOldLogic();
            else if (RenumberAll) RenumberAllLogic();
            else if (RenumberSelected) RenumberSelectedLogic();

            UpdateRowColors();
        }

        private void CompareToOldLogic()
        {
            var usedSeqs = Groups.Where(g => g.Seq > 0 && g.Seq != 0.001).Select(g => g.Seq).ToList();
            
            foreach (var g in Groups)
            {
                var existing = g.ExistingSequences;
                bool isNull = existing.All(x => x == 0);

                if (existing.Count == 1 && existing[0] == 0)
                {
                    double next = 1;
                    while (usedSeqs.Contains(next)) next++;
                    g.Seq = next;
                    usedSeqs.Add(next);
                    g.Note = "";
                }
                else if (existing.Count > 1 && isNull)
                {
                    double next = 1;
                    while (usedSeqs.Contains(next)) next++;
                    g.Seq = next;
                    usedSeqs.Add(next);
                    g.Note = "";
                }
                else if (existing.Count > 1 && !isNull)
                {
                    double min = existing.Min();
                    while (usedSeqs.Contains(min)) min++;
                    g.Seq = min;
                    usedSeqs.Add(min);
                    g.Note = "";
                }
            }
            CheckForDuplicates();
        }

        private void RenumberAllLogic()
        {
            if (!double.TryParse(StartNumberStr, out double start))
            {
                MessageBox.Show("Enter valid start numbering!");
                return;
            }

            foreach (var g in Groups)
            {
                g.Seq = start++;
                g.Note = "";
            }
            CheckForDuplicates();
        }

        private void RenumberSelectedLogic()
        {
            // This requires knowledge of selected items in View. 
            // In MVVM we usually bind IsSelected property of GroupItem.
            var selected = Groups.Where(g => g.IsSelected).OrderBy(g => Groups.IndexOf(g)).ToList();
            if (selected.Count == 0)
            {
                MessageBox.Show("Select rows in the grid first!");
                return;
            }

            if (!double.TryParse(StartNumberStr, out double start))
            {
                MessageBox.Show("Enter valid start numbering!");
                return;
            }

            foreach (var g in selected)
            {
                g.Seq = start++;
                g.Note = "";
            }
            CheckForDuplicates();
        }

        private void CheckForDuplicates()
        {
            var groupsArr = Groups.ToArray();
            for (int i = 0; i < groupsArr.Length; i++)
            {
                if (groupsArr[i].Note == "Check again") groupsArr[i].Note = "";

                bool hasDuplicate = false;
                for (int j = 0; j < groupsArr.Length; j++)
                {
                    if (i == j) continue;
                    if (groupsArr[i].Seq == groupsArr[j].Seq && groupsArr[i].Seq != 0 && groupsArr[i].Seq != 0.001)
                    {
                        hasDuplicate = true;
                        break;
                    }
                }
                if (hasDuplicate && string.IsNullOrEmpty(groupsArr[i].Note)) 
                    groupsArr[i].Note = "Check again";
            }
        }

        private void UpdateRowColors()
        {
            foreach (var g in Groups)
            {
                if (g.Seq == 0 || g.Seq == 0.001 || g.Note == "Check again" || g.Note == "Overlap")
                    g.ColorBrush = "#CCE5FF"; // Light blue match original Color.FromArgb(204, 229, 255)
                else
                    g.ColorBrush = "Transparent";
            }
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

        public void UpdateInModel()
        {
            if (Groups.Count == 0) return;

            int count = 0;
            foreach (var g in Groups)
            {
                foreach (var rebar in g.BackingRebars)
                {
                    // Update Tekla UDA
                    rebar.SetUserProperty("REBAR_SEQ_NO", (int)g.Seq);
                    rebar.Modify();
                    count++;
                }
            }
            _model.CommitChanges();
            MessageBox.Show($"Updated {count} rebars in Tekla model.");
            
            // Refresh
            GetRebarsFromModel();
        }
    }

    public class RSQNGroupItem : INotifyPropertyChanged
    {
        private double _seq;
        private string _note;
        private bool _isSelected;
        private string _colorBrush = "Transparent";

        public string Name { get; set; }
        public string Mark { get; set; }
        public double Seq { get => _seq; set { _seq = value; OnPropertyChanged(); } }
        public string Grade { get; set; }
        public string Size { get; set; }
        public int Quantity { get; set; }
        public double Length { get; set; }
        public double Weight { get; set; }
        public string Note { get => _note; set { _note = value; OnPropertyChanged(); } }

        public List<TS.Identifier> Identifiers { get; set; } = new List<TS.Identifier>();
        public List<Reinforcement> BackingRebars { get; set; } = new List<Reinforcement>();
        public List<double> ExistingSequences { get; set; } = new List<double>();

        public bool IsSelected { get => _isSelected; set { _isSelected = value; OnPropertyChanged(); } }
        public string ColorBrush { get => _colorBrush; set { _colorBrush = value; OnPropertyChanged(); } }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
