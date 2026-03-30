using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Tekla.Structures.Model;
using System.Collections.ObjectModel;
using Tekla.Structures.Model.UI;
using TeklaApp.Helpers;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    public class RebarNumberingViewModel : INotifyPropertyChanged
    {
        private TeklaModelMng _teklaModel;
        private RebarNumberingModel _logicModel;
        private string _statusMessage = "Ready to Number selected Rebars";
        private int _startingNumber = 1;
        private bool _isAutoPrefixEnabled = true;

        private string _slabKeywords = "SLAB,SÀN,FLOOR";
        private string _beamKeywords = "TB,DẦM,BEAM";
        private string _wallKeywords = "TW,SW,VÁCH,WALL";
        private ObservableCollection<SizeColorItem> _sizeColorTable;

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int StartingNumber
        {
            get => _startingNumber;
            set { _startingNumber = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public bool IsAutoPrefixEnabled
        {
            get => _isAutoPrefixEnabled;
            set { _isAutoPrefixEnabled = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public string SlabKeywords
        {
            get => _slabKeywords;
            set { _slabKeywords = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public string BeamKeywords
        {
            get => _beamKeywords;
            set { _beamKeywords = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public string WallKeywords
        {
            get => _wallKeywords;
            set { _wallKeywords = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public ObservableCollection<SizeColorItem> SizeColorTable
        {
            get => _sizeColorTable;
            set { _sizeColorTable = value; OnPropertyChanged(); }
        }

        public RebarNumberingViewModel()
        {
            _teklaModel = new TeklaModelMng();
            _logicModel = new RebarNumberingModel();
            LoadPersistentSettings();
        }

        private void LoadPersistentSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                _startingNumber = int.TryParse(settings.StartingNumber, out int n) ? n : 1;
                _slabKeywords = settings.SlabKeywords;
                _beamKeywords = settings.BeamKeywords;
                _wallKeywords = settings.WallKeywords;

                // Parse SizeClassMapping
                _sizeColorTable = new ObservableCollection<SizeColorItem>();
                if (!string.IsNullOrEmpty(settings.SizeClassMapping))
                {
                    var parts = settings.SizeClassMapping.Split(';');
                    foreach (var p in parts)
                    {
                        var pair = p.Split(':');
                        if (pair.Length == 2 && int.TryParse(pair[0], out int sz) && int.TryParse(pair[1], out int cl))
                        {
                            _sizeColorTable.Add(new SizeColorItem { RebarSize = sz, RebarClass = cl });
                        }
                    }
                }

                // Refresh bindings
                OnPropertyChanged(nameof(StartingNumber));
                OnPropertyChanged(nameof(SlabKeywords));
                OnPropertyChanged(nameof(BeamKeywords));
                OnPropertyChanged(nameof(WallKeywords));
                OnPropertyChanged(nameof(SizeColorTable));
            }
            catch { }
        }

        private void SavePersistentSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                settings.StartingNumber = StartingNumber.ToString();
                settings.SlabKeywords = SlabKeywords;
                settings.BeamKeywords = BeamKeywords;
                settings.WallKeywords = WallKeywords;

                // Serialize SizeColorTable
                if (SizeColorTable != null)
                {
                    settings.SizeClassMapping = string.Join(";", SizeColorTable.Select(x => $"{x.RebarSize}:{x.RebarClass}"));
                }

                SettingsService.SaveSettings(settings);
            }
            catch { }
        }

        public void RunAutoPrefix()
        {
            if (!_teklaModel.IsConnected())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            try
            {
                SavePersistentSettings();
                // 1. Get current selection of PARTS or prompt picker
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();
                List<Part> selectedParts = new List<Part>();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Part part) selectedParts.Add(part);
                }

                if (selectedParts.Count == 0)
                {
                    Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                    StatusMessage = "Select Parts/Beams/Slabs to assign Prefix to their rebars...";
                    var pickedEnum = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_PARTS, "Select Parts/Beams/Slabs");
                    while (pickedEnum.MoveNext())
                    {
                        if (pickedEnum.Current is Part part) selectedParts.Add(part);
                    }
                }

                if (selectedParts.Count == 0)
                {
                    StatusMessage = "No parts selected.";
                    return;
                }

                int totalRebarsProcessed = 0;
                foreach (var part in selectedParts)
                {
                    List<Reinforcement> partRebars = GetRebarsOfPart(part);
                    foreach (var rebar in partRebars)
                    {
                        _logicModel.AutoAssignPrefix(rebar, part, SlabKeywords, BeamKeywords, WallKeywords);
                        totalRebarsProcessed++;
                    }
                }

                _teklaModel.GetModel().CommitChanges();
                StatusMessage = $"Success! Assigned Auto-Prefix to {totalRebarsProcessed} rebars across {selectedParts.Count} parts.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        private List<Reinforcement> GetRebarsOfPart(Part part)
        {
            List<Reinforcement> rebars = new List<Reinforcement>();

            // 1. Direct children
            var children = part.GetChildren();
            while (children.MoveNext())
            {
                if (children.Current is Reinforcement rebar) rebars.Add(rebar);
            }

            // 2. Assembly content
            var assembly = part.GetAssembly();
            if (assembly != null)
            {
                var secondaries = assembly.GetSecondaries();
                foreach (var obj in secondaries)
                {
                    if (obj is Reinforcement rebar && !rebars.Contains(rebar)) rebars.Add(rebar);
                }
            }

            return rebars;
        }

        public void RunNumbering()
        {
            if (!_teklaModel.IsConnected())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            try
            {
                SavePersistentSettings();
                Model model = _teklaModel.GetModel();

                // 1. Try to get current selection
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();

                List<Reinforcement> selectedRebars = new List<Reinforcement>();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Reinforcement rebar)
                    {
                        selectedRebars.Add(rebar);
                    }
                }

                // 2. If nothing was selected, fallback to Picker
                if (selectedRebars.Count == 0)
                {
                    Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                    StatusMessage = "No selection. Please sweep select rebars to number...";
                    var pickedEnum = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_REINFORCEMENTS, "Sweep select rebars to number");

                    while (pickedEnum.MoveNext())
                    {
                        if (pickedEnum.Current is Reinforcement rebar)
                        {
                            selectedRebars.Add(rebar);
                        }
                    }
                }

                if (selectedRebars.Count == 0)
                {
                    StatusMessage = "No rebar objects selected.";
                    return;
                }

                StatusMessage = $"Numbering {selectedRebars.Count} selected rebars...";

                // Logic: 
                // 1. Group rebar by Signature (shape/size/hooks) + Prefix
                // 2. Identify existing numbers in the selection
                // 3. Assign numbers to groups
                //    - If group has an existing number (>0), keep it (if not conflicted)
                //    - Otherwise, assign next available number starting from StartingNumber

                var groupSets = selectedRebars.GroupBy(r =>
                                     {
                                         string sig = _logicModel.GetRebarSignature(r);
                                         string prefix = r.NumberingSeries?.Prefix ?? "";
                                         return $"{sig}|{prefix}";
                                     })
                                     .ToList();

                Dictionary<string, int> groupToNumberMap = new Dictionary<string, int>();
                HashSet<int> usedNumbers = new HashSet<int>();

                // First pass: Collect groups that already have a number
                foreach (var group in groupSets)
                {
                    int existingNum = 0;
                    foreach (var rebar in group)
                    {
                        int val = 0;
                        rebar.GetUserProperty("REBAR_SEQ_NO", ref val);
                        if (val > 0)
                        {
                            existingNum = val;
                            break; // Take the first non-zero number found in the group
                        }
                    }

                    if (existingNum > 0 && !usedNumbers.Contains(existingNum))
                    {
                        groupToNumberMap[group.Key] = existingNum;
                        usedNumbers.Add(existingNum);
                    }
                    else
                    {
                        groupToNumberMap[group.Key] = 0; // Mark for re-numbering
                    }
                }

                // Second pass: Assign numbers to group with 0 (new bars or conflicted bars)
                int nextNum = StartingNumber;
                foreach (var groupKey in groupSets.Select(g => g.Key))
                {
                    if (groupToNumberMap[groupKey] == 0)
                    {
                        // Find next available number
                        while (usedNumbers.Contains(nextNum))
                        {
                            nextNum++;
                        }
                        groupToNumberMap[groupKey] = nextNum;
                        usedNumbers.Add(nextNum);
                    }
                }

                // Apply changes to rebars
                int updatedCount = 0;
                foreach (var group in groupSets)
                {
                    int finalNum = groupToNumberMap[group.Key];
                    foreach (var rebar in group)
                    {
                        rebar.SetUserProperty("REBAR_SEQ_NO", finalNum);
                        rebar.Modify();
                        updatedCount++;
                    }
                }

                model.CommitChanges();
                StatusMessage = $"Success! Numbered {selectedRebars.Count} rebars into {groupSets.Count} unique groups.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        public void RunColorBySize()
        {
            if (!_teklaModel.IsConnected())
            {
                StatusMessage = "Error: Tekla not connected.";
                return;
            }

            try
            {
                SavePersistentSettings(); // Ensure settings are saved if user edited table

                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();
                List<Reinforcement> selectedRebars = new List<Reinforcement>();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Reinforcement rebar) selectedRebars.Add(rebar);
                }

                if (selectedRebars.Count == 0)
                {
                    Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                    var pickedEnum = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_REINFORCEMENTS, "Select rebars to assign color by size");
                    while (pickedEnum.MoveNext())
                    {
                        if (pickedEnum.Current is Reinforcement rebar) selectedRebars.Add(rebar);
                    }
                }

                if (selectedRebars.Count == 0)
                {
                    StatusMessage = "No rebar objects selected.";
                    return;
                }

                var map = SizeColorTable.ToDictionary(x => x.RebarSize.ToString(), x => x.RebarClass);
                int count = 0;
                foreach (var rebar in selectedRebars)
                {
                    string size = "";
                    rebar.GetReportProperty("SIZE", ref size);

                    // Sizes might be like "10" or "D10" or "T10", we need to extract the number if possible or use a more robust matching
                    // For now, let's assume it matches the mapping strings or try to extract numerical part
                    string numericPart = new string(size.Where(char.IsDigit).ToArray());
                    if (string.IsNullOrEmpty(numericPart)) numericPart = size;

                    if (map.TryGetValue(numericPart, out int targetClass))
                    {
                        rebar.Class = targetClass;
                        rebar.Modify();
                        count++;
                    }
                }

                _teklaModel.GetModel().CommitChanges();
                StatusMessage = $"Success! Applied Class/Color mapping to {count} out of {selectedRebars.Count} rebars.";
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }

    public class SizeColorItem
    {
        public int RebarSize { get; set; }
        public int RebarClass { get; set; }
    }
}
