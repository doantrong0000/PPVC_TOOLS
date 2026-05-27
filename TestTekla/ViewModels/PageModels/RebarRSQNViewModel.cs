using Fusion;
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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TS = Tekla.Structures;
using TSM = Tekla.Structures.Model;

namespace TeklaApp.ViewModels
{
    public class RebarRSQNViewModel : INotifyPropertyChanged
    {
        private readonly Model _model = new Model();
        public ObservableCollection<RSQNGroupItem> Groups { get; set; } = new ObservableCollection<RSQNGroupItem>();
        private bool _renumberSelected;
        private string _statusMessage = "Ready";
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
                foreach (var g in groupedData.OrderBy(x => x.Seq == 0 ? 0 : 1).ThenBy(x => x.Seq))
                {
                    Groups.Add(g);
                }

                // CheckForDuplicates(); // Removed as per request to separate into buttons
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

        public void CheckAgainAndSort()
        {
            GetRebarsFromModel();

            var groupsList = Groups.ToList();
            foreach (var g in groupsList)
            {
                if (g.Note == "Check again") continue; // Skip already flagged items
                var list = new List<RSQNGroupItem>();
                foreach (var otherRebar in Groups)
                {

                    if (g == otherRebar) continue;
                    if (otherRebar.Seq == g.Seq) list.Add(otherRebar);
                }
                bool check = false;
                foreach (var item in list)
                {
                    if (item.Shape != g.Shape)
                    {
                        item.Note = "Check again";
                        check = true;
                        continue;
                    }
                    if (item.Length != g.Length)
                    {
                        item.Note = "Check again";
                        check = true;
                        continue;
                    }
                    if (item.Grade != g.Grade)
                    {
                        item.Note = "Check again";
                        check = true;
                        continue;
                    }
                    if (Math.Round(item.Weight, 3) != Math.Round(g.Weight, 3))
                    {
                        item.Note = "Check again";
                        check = true;
                        continue;
                    }
                }
                if (check)
                {
                    g.Note = "Check again";
                }


            }

            Groups.Clear();

            // Sort so flagged items are at the top
            var sorted = groupsList.OrderBy(g => g.Note == "Check again" ? 0 : 1)
                                   .ThenBy(g => g.Seq).ToList();


            foreach (var item in sorted) Groups.Add(item);

            UpdateRowColors("CheckAgain");
        }

        public void CheckOverlapAndSort()
        {
            GetRebarsFromModel();

            int found = 0;
            foreach (var rebar in Groups)
            {
                if (rebar.Note == "Overlap") continue; // Skip already flagged items
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
                        item.Note = "Overlap";
                        banThemCoBiOverlapKhong = true; // Đánh dấu là đã phát hiện lỗi
                    }
                }
                if (banThemCoBiOverlapKhong)
                {
                    rebar.Note = "Overlap";
                }
            }

            // Sort so flagged items are at the top
            var sorted = Groups.OrderBy(g => g.Note == "Overlap" ? 0 : 1)
                .ThenBy(g => g.Length).ThenBy(g => g.Shape)
                               .ThenBy(g => g.Seq).ToList();

            Groups.Clear();
            foreach (var item in sorted) Groups.Add(item);

            UpdateRowColors("Overlap");
        }

        private void UpdateRowColors(string mode = "All")
        {
            foreach (var g in Groups)
            {
                if (mode == "CheckAgain")
                {
                    if (g.Note == "Check again") g.ColorBrush = "#FFF2CC"; // Light yellow
                    else g.ColorBrush = "Transparent";
                }
                else if (mode == "Overlap")
                {
                    if (g.Note == "Overlap") g.ColorBrush = "#FFCDD2"; // Light red
                    else g.ColorBrush = "Transparent";
                }
                else
                {
                    if (g.Note == "Check again")
                        g.ColorBrush = "#FFF2CC"; // Light yellow
                    else if (g.Seq <= 0.001 || g.Note == "Overlap")
                        g.ColorBrush = "#FFCDD2"; // Light red
                    else
                        g.ColorBrush = "Transparent";
                }
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
    }

    public class RSQNGroupItem : INotifyPropertyChanged
    {
        private double _seq;
        private string _note;
        private bool _isSelected;
        private string _colorBrush = "Transparent";

        public string Name { get; set; }
        public string Mark { get; set; }
        public double Seq
        {
            get => _seq;
            set
            {
                _seq = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SeqDisplay));
            }
        }
        public string SeqDisplay
        {
            get
            {
                if (Note == "Overlap" && ExistingSequences.Count > 1)
                    return string.Join(", ", ExistingSequences);
                if (Seq == 0) return "";
                if (Seq == 0.001) return string.Join(", ", ExistingSequences);
                return Seq.ToString();
            }
        }
        public string Grade { get; set; }
        public string Size { get; set; }
        public string Shape { get; set; }
        public int Quantity { get; set; }
        public double Length { get; set; }
        public double Weight { get; set; }
        public string Note
        {
            get => _note;
            set
            {
                _note = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(SeqDisplay));
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
