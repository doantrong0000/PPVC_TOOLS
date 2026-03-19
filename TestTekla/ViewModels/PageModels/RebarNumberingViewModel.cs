using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using Tekla.Structures.Model;
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

        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
        }

        public int StartingNumber
        {
            get => _startingNumber;
            set { _startingNumber = value; OnPropertyChanged(); }
        }

        public bool IsAutoPrefixEnabled
        {
            get => _isAutoPrefixEnabled;
            set { _isAutoPrefixEnabled = value; OnPropertyChanged(); }
        }

        public string SlabKeywords
        {
            get => _slabKeywords;
            set { _slabKeywords = value; OnPropertyChanged(); }
        }

        public string BeamKeywords
        {
            get => _beamKeywords;
            set { _beamKeywords = value; OnPropertyChanged(); }
        }

        public string WallKeywords
        {
            get => _wallKeywords;
            set { _wallKeywords = value; OnPropertyChanged(); }
        }

        public RebarNumberingViewModel()
        {
            _teklaModel = new TeklaModelMng();
            _logicModel = new RebarNumberingModel();
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

                // Grouping logic: name, size, hook, length, shape + existing Prefix
                var groups = selectedRebars.GroupBy(r => 
                                         {
                                             string sig = _logicModel.GetRebarSignature(r);
                                             string prefix = r.NumberingSeries?.Prefix ?? "";
                                             return $"{sig}|{prefix}";
                                         })
                                         .OrderBy(g => g.Key)
                                         .ToList();

                int currentNum = StartingNumber;

                foreach (var group in groups)
                {
                    foreach (var rebar in group)
                    {
                        bool success = rebar.SetUserProperty("REBAR_SEQ_NO", currentNum);
                        if (success)
                        {
                            rebar.Modify();
                        }
                    }
                    currentNum++;
                }

                model.CommitChanges();
                StatusMessage = $"Success! Numbered {selectedRebars.Count} rebars into {groups.Count} unique groups.";
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
}
