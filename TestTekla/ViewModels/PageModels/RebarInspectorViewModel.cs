using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using TeklaApp.Helpers;
using TeklaApp.Models;

namespace TeklaApp.ViewModels
{
    public class RebarInspectorViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<RebarInfoItem> Rebars { get; set; } = new ObservableCollection<RebarInfoItem>();
        public string SelectedObjectName { get; set; } = "";
        private Model _model = new Model();
        private TeklaModelMng _teklaModel = new TeklaModelMng();
        private RebarNumberingModel _logicModel = new RebarNumberingModel();

        private int _startingNumber = 1;
        private string _slabKeywords = "SLAB,SÀN,FLOOR";
        private string _beamKeywords = "TB,DẦM,BEAM";
        private string _wallKeywords = "TW,SW,VÁCH,WALL";

        public int StartingNumber
        {
            get => _startingNumber;
            set { _startingNumber = value; OnPropertyChanged(); SavePersistentSettings(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public RebarInspectorViewModel()
        {
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
                OnPropertyChanged(nameof(StartingNumber));
            }
            catch { }
        }

        private void SavePersistentSettings()
        {
            try
            {
                var settings = SettingsService.LoadSettings();
                settings.StartingNumber = StartingNumber.ToString();
                SettingsService.SaveSettings(settings);
            }
            catch { }
        }

        private bool MatchesKeywords(string name, string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords)) return false;
            var keys = keywords.Split(',').Select(k => k.Trim().ToUpper()).Where(k => !string.IsNullOrEmpty(k));
            foreach (var key in keys)
            {
                if (name.Contains(key)) return true;
            }
            return false;
        }

        public string RunNumbering(bool reassignAll = false)
        {
            if (!_teklaModel.IsConnected())
            {
                return "Error: Tekla not connected.";
            }

            try
            {
                SavePersistentSettings();
                LoadPersistentSettings(); // refresh keywords if changed elsewhere

                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                var enumerator = selector.GetSelectedObjects();

                List<Reinforcement> selectedRebars = new List<Reinforcement>();
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current is Reinforcement rebar) selectedRebars.Add(rebar);
                    else if (enumerator.Current is Part part)
                    {
                        var children = part.GetChildren();
                        while (children.MoveNext())
                        {
                            if (children.Current is Reinforcement rebarChild) selectedRebars.Add(rebarChild);
                        }
                    }
                }

                if (selectedRebars.Count == 0)
                {
                    Tekla.Structures.Model.UI.Picker picker = new Tekla.Structures.Model.UI.Picker();
                    var pickedEnum = picker.PickObjects(Tekla.Structures.Model.UI.Picker.PickObjectsEnum.PICK_N_OBJECTS, "Sweep select rebars or parts to number");

                    while (pickedEnum.MoveNext())
                    {
                        if (pickedEnum.Current is Reinforcement rebar) selectedRebars.Add(rebar);
                        else if (pickedEnum.Current is Part part)
                        {
                            var children = part.GetChildren();
                            while (children.MoveNext())
                            {
                                if (children.Current is Reinforcement rebarChild) selectedRebars.Add(rebarChild);
                            }
                        }
                    }
                }

                if (selectedRebars.Count == 0)
                {
                    return "No rebar objects selected.";
                }

                // Group rebars by Signature + Prefix
                var groupSets = selectedRebars.GroupBy(r =>
                {
                    string sig = _logicModel.GetRebarSignature(r);
                    string prefix = r.NumberingSeries?.Prefix ?? "";
                    return $"{sig}|{prefix}";
                }).ToList();

                // Build a list of info for sorting groups
                var groupSortData = new List<RebarNumberingGroupInfo>();

                foreach (var group in groupSets)
                {
                    var firstRebar = group.First();

                    // Determine Host Part
                    string hostName = "";
                    int hostId = 0;

                    ModelObject parent = firstRebar.GetFatherComponent();
                    if (parent is Part p)
                    {
                        hostName = p.Name ?? p.Profile.ProfileString;
                        hostId = p.Identifier.ID;
                    }
                    else
                    {
                        firstRebar.GetReportProperty("MAIN_PART.NAME", ref hostName);
                    }
                    hostName = hostName.ToUpper();

                    // Determine Part Type priority: 1=Beam, 2=Slab/Roof, 3=Wall, 4=Other
                    int typePriority = 4;
                    if (MatchesKeywords(hostName, _beamKeywords)) typePriority = 1;
                    else if (MatchesKeywords(hostName, _slabKeywords)) typePriority = 2;
                    else if (MatchesKeywords(hostName, _wallKeywords)) typePriority = 3;

                    // Determine Length (for descending sort)
                    double length = 0;
                    firstRebar.GetReportProperty("LENGTH", ref length);

                    groupSortData.Add(new RebarNumberingGroupInfo
                    {
                        Key = group.Key,
                        Rebars = group.ToList(),
                        PartTypePriority = typePriority,
                        HostId = hostId,
                        HostName = hostName,
                        Length = length
                    });
                }

                // Sort: 1. Part Type (Asc) -> 2. Host (Asc) -> 3. Length (Desc)
                var sortedGroups = groupSortData
                    .OrderBy(g => g.PartTypePriority)
                    .ThenBy(g => g.HostName)
                    .ThenBy(g => g.HostId)
                    .ThenByDescending(g => g.Length)
                    .ToList();

                Dictionary<string, int> groupToNumberMap = new Dictionary<string, int>();
                HashSet<int> usedNumbers = new HashSet<int>();

                if (!reassignAll)
                {
                    // First pass: Collect groups that already have a number > 0
                    foreach (var gInfo in sortedGroups)
                    {
                        int existingNum = 0;
                        foreach (var rebar in gInfo.Rebars)
                        {
                            int val = 0;
                            rebar.GetUserProperty("REBAR_SEQ_NO", ref val);
                            if (val > 0)
                            {
                                existingNum = val;
                                break;
                            }
                        }

                        if (existingNum > 0 && !usedNumbers.Contains(existingNum))
                        {
                            groupToNumberMap[gInfo.Key] = existingNum;
                            usedNumbers.Add(existingNum);
                        }
                        else
                        {
                            groupToNumberMap[gInfo.Key] = 0; // mark for assigning
                        }
                    }
                }
                else
                {
                    // Force reassign all
                    foreach (var gInfo in sortedGroups)
                    {
                        groupToNumberMap[gInfo.Key] = 0;
                    }
                }

                int nextNum = StartingNumber;

                // Second pass: Assign strictly by Priority Order
                foreach (var gInfo in sortedGroups)
                {
                    if (groupToNumberMap[gInfo.Key] == 0)
                    {
                        while (usedNumbers.Contains(nextNum))
                        {
                            nextNum++;
                        }
                        groupToNumberMap[gInfo.Key] = nextNum;
                        usedNumbers.Add(nextNum);
                    }
                }

                // Apply changes
                int updatedCount = 0;
                foreach (var gInfo in sortedGroups)
                {
                    int finalNum = groupToNumberMap[gInfo.Key];
                    foreach (var rebar in gInfo.Rebars)
                    {
                        rebar.SetUserProperty("REBAR_SEQ_NO", finalNum);
                        rebar.Modify();
                        updatedCount++;
                    }
                }

                _teklaModel.GetModel().CommitChanges();
                return $"Success! Numbered {selectedRebars.Count} rebars into {sortedGroups.Count} unique groups.";
            }
            catch (Exception ex)
            {
                return "Error: " + ex.Message;
            }
        }

        public List<RebarInfoItem> GetRebarData(out string status)
        {
            List<RebarInfoItem> results = new List<RebarInfoItem>();
            status = "Ready";

            if (!_model.GetConnectionStatus())
            {
                status = "Error: Tekla Model not connected.";
                return results;
            }

            Picker picker = new Picker();
            try
            {
                List<ModelObject> targetObjects = new List<ModelObject>();

                // 1. Check for PRE-SELECTION
                Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                ModelObjectEnumerator selectedObjects = selector.GetSelectedObjects();

                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is Part || selectedObjects.Current is Reinforcement)
                    {
                        targetObjects.Add(selectedObjects.Current);
                    }
                }

                // 2. If nothing selected, then PICK manually (Multiple)
                if (targetObjects.Count == 0)
                {
                    status = "Picking objects...";
                    ModelObjectEnumerator pickedEnum = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_PARTS, "Select parts or rebars to inspect (Esc to finish)");
                    while (pickedEnum.MoveNext())
                    {
                        targetObjects.Add(pickedEnum.Current);
                    }
                }

                if (targetObjects.Count == 0)
                {
                    status = "No objects selected.";
                    return results;
                }

                // Update UI header with first object name or count
                if (targetObjects.Count == 1)
                {
                    var first = targetObjects[0];
                    SelectedObjectName = (first is Part p) ? (string.IsNullOrEmpty(p.Name) ? p.Profile.ProfileString : p.Name) : "Selected Rebar";
                }
                else
                {
                    SelectedObjectName = $"{targetObjects.Count} Objects Selected";
                }

                Dictionary<string, Reinforcement> uniqueRebars = new Dictionary<string, Reinforcement>();
                Dictionary<string, string> rebarToHostMap = new Dictionary<string, string>();

                // 1. Lấy thép từ các Part trước tiên để đảm bảo có host chính xác
                foreach (var obj in targetObjects)
                {
                    if (obj is Part part)
                    {
                        ModelObjectEnumerator children = part.GetChildren();
                        while (children.MoveNext())
                        {
                            if (children.Current is Reinforcement rebar)
                            {
                                string id = rebar.Identifier.ID.ToString();
                                if (!uniqueRebars.ContainsKey(id))
                                {
                                    uniqueRebars[id] = rebar;
                                    rebarToHostMap[id] = string.IsNullOrEmpty(part.Name) ? part.Profile.ProfileString : part.Name;
                                }
                            }
                        }
                    }
                }

                // 2. Xét các thép (Reinforcement) được chọn rời mà chưa có trong danh sách
                foreach (var obj in targetObjects)
                {
                    if (obj is Reinforcement rebar)
                    {
                        string id = rebar.Identifier.ID.ToString();
                        if (!uniqueRebars.ContainsKey(id))
                        {
                            uniqueRebars[id] = rebar;

                            string hostName = "";

                            // Thử lấy qua cha trực tiếp (Father) trong cấu trúc cây của Tekla
                            ModelObject parent = rebar.GetFatherComponent();
                            if (parent is Part p)
                            {
                                hostName = string.IsNullOrEmpty(p.Name) ? p.Profile.ProfileString : p.Name;
                            }

                            // Nếu GetFatherComponent không ra, dùng Report Property dự phòng
                            if (string.IsNullOrEmpty(hostName))
                            {
                                rebar.GetReportProperty("MAIN_PART.NAME", ref hostName);
                            }

                            if (string.IsNullOrEmpty(hostName))
                            {
                                rebar.GetReportProperty("FATHER.NAME", ref hostName);
                            }

                            rebarToHostMap[id] = string.IsNullOrEmpty(hostName) ? "Không có part" : hostName;
                        }
                    }
                }

                foreach (var kvp in uniqueRebars)
                {
                    var r = kvp.Value;
                    string id = kvp.Key;

                    string name = ""; r.GetReportProperty("NAME", ref name);
                    string size = ""; r.GetReportProperty("SIZE", ref size);
                    string grade = ""; r.GetReportProperty("GRADE", ref grade);
                    string pos = ""; r.GetReportProperty("REBAR_POS", ref pos);

                    // Rebar Sequence Number (REBAR_SEQ_NO) thường là kiểu số nguyên (int) trong UDA
                    string seq = "";
                    int seqInt = 0;
                    if (r.GetUserProperty("REBAR_SEQ_NO", ref seqInt))
                    {
                        seq = seqInt.ToString();
                    }
                    else if (!r.GetUserProperty("REBAR_SEQ_NO", ref seq))
                    {
                        // Phương án dự phòng qua Report Property
                        r.GetReportProperty("USERDEFINED.REBAR_SEQ_NO", ref seq);
                    }

                    string spacing = "---";

                    // Improved Quantity retrieval
                    int dQty = 0;
                    r.GetReportProperty("NUMBER", ref dQty);
                    if (r is RebarGroup group)
                    {
                        if (group.Spacings != null && group.Spacings.Count > 0)
                        {
                            var spacingList = group.Spacings.Cast<double>().Where(s => s > 0).Select(s => Math.Round(s, 0)).ToList();
                            if (spacingList.Count > 0)
                                spacing = spacingList.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key.ToString();
                        }
                    }

                    results.Add(new RebarInfoItem
                    {
                        Name = name,
                        Size = size,
                        Grade = grade,
                        Position = pos,
                        Seq = seq,
                        Quantity = dQty,
                        Id = id,
                        TargetSpacing = spacing,
                        HostName = rebarToHostMap[id]
                    });
                }

                status = $"Showing {results.Count} rebar entries from {targetObjects.Count} objects.";
                return results;
            }
            catch (Exception) { status = "Cancelled."; return results; }
        }

        public void ExportToJson()
        {
            if (Rebars.Count == 0) return;

            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "JSON Files (*.json)|*.json";
            saveFileDialog.FileName = "RebarData_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";

            if (saveFileDialog.ShowDialog() == true)
            {
                try
                {
                    // Structuring data for better AI readability
                    var exportData = new
                    {
                        ExportDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        ObjectCount = SelectedObjectName,
                        Rebars = Rebars.OrderBy(x => x.Position).ThenBy(x => x.Seq).ToList()
                    };

                    string json = JsonConvert.SerializeObject(exportData, Formatting.Indented);
                    File.WriteAllText(saveFileDialog.FileName, json);
                    System.Windows.MessageBox.Show("Exported successful to " + saveFileDialog.FileName, "Success", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show("Error exporting JSON: " + ex.Message, "Error", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
                }
            }
        }

        public void SelectRebarInTekla(string idString)
        {
            if (int.TryParse(idString, out int id))
            {
                Tekla.Structures.Identifier identifier = new Tekla.Structures.Identifier(id);
                ModelObject modelObject = _model.SelectModelObject(identifier);
                if (modelObject != null)
                {
                    System.Collections.ArrayList objects = new System.Collections.ArrayList { modelObject };
                    Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    selector.Select(objects);
                }
            }
        }
    }

    public class RebarInfoItem
    {
        public string Position { get; set; }
        public string Seq { get; set; }
        public string Name { get; set; }
        public string Size { get; set; }
        public string Grade { get; set; }
        public int Quantity { get; set; }
        public string TargetSpacing { get; set; }
        public string HostName { get; set; }
        public string Id { get; set; }
    }

    public class RebarNumberingGroupInfo
    {
        public string Key { get; set; }
        public List<Reinforcement> Rebars { get; set; }
        public int PartTypePriority { get; set; }
        public string HostName { get; set; }
        public int HostId { get; set; }
        public double Length { get; set; }
    }
}

