using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.IO;
using Microsoft.Win32;
using Newtonsoft.Json;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Geometry3d;
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

        /// <summary>Preview numbering: calculates SEQ numbers and updates the grid only (no Tekla write)</summary>
        public string PreviewNumbering(bool reassignAll = false)
        {
            if (Rebars.Count == 0)
                return "No rebars loaded. Pick part first.";

            if (!_model.GetConnectionStatus())
                return "Error: Tekla not connected.";

            try
            {
                LoadPersistentSettings();

                // Build Reinforcement objects from loaded IDs
                var rebarMap = new Dictionary<string, Reinforcement>();
                foreach (var item in Rebars)
                {
                    if (!int.TryParse(item.Id, out int objId)) continue;
                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (obj is Reinforcement r) rebarMap[item.Id] = r;
                }

                if (rebarMap.Count == 0) return "No valid rebar objects found.";

                // Group by signature (Geometry-only grouping, ignoring Prefix/Position)
                var groupSets = rebarMap.GroupBy(kv => _logicModel.GetRebarSignature(kv.Value)).ToList();

                // Build sorting data
                var groupSortData = new List<RebarNumberingGroupInfo>();
                foreach (var group in groupSets)
                {
                    var firstRebar = group.First().Value;
                    string hostName = "";
                    int hostId = 0;
                    ModelObject parent = firstRebar.GetFatherComponent();
                    if (parent is Part p) { hostName = p.Name ?? ""; hostId = p.Identifier.ID; }
                    else { firstRebar.GetReportProperty("MAIN_PART.NAME", ref hostName); }
                    hostName = hostName.ToUpper();

                    int typePriority = 4;
                    if (MatchesKeywords(hostName, _beamKeywords)) typePriority = 1;
                    else if (MatchesKeywords(hostName, _slabKeywords)) typePriority = 2;
                    else if (MatchesKeywords(hostName, _wallKeywords)) typePriority = 3;

                    double length = 0;
                    firstRebar.GetReportProperty("LENGTH", ref length);

                    groupSortData.Add(new RebarNumberingGroupInfo
                    {
                        Key = group.Key,
                        Rebars = group.Select(kv => kv.Value).ToList(),
                        RebarIds = group.Select(kv => kv.Key).ToList(),
                        PartTypePriority = typePriority,
                        HostId = hostId,
                        HostName = hostName,
                        Length = length
                    });
                }

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
                    foreach (var gInfo in sortedGroups)
                    {
                        int existingNum = 0;
                        foreach (var rebar in gInfo.Rebars)
                        {
                            int val = 0;
                            rebar.GetUserProperty("REBAR_SEQ_NO", ref val);
                            if (val > 0) { existingNum = val; break; }
                        }
                        if (existingNum > 0 && !usedNumbers.Contains(existingNum))
                        {
                            groupToNumberMap[gInfo.Key] = existingNum;
                            usedNumbers.Add(existingNum);
                        }
                        else { groupToNumberMap[gInfo.Key] = 0; }
                    }
                }
                else
                {
                    foreach (var gInfo in sortedGroups) groupToNumberMap[gInfo.Key] = 0;
                }

                int nextNum = StartingNumber;
                foreach (var gInfo in sortedGroups)
                {
                    if (groupToNumberMap[gInfo.Key] == 0)
                    {
                        while (usedNumbers.Contains(nextNum)) nextNum++;
                        groupToNumberMap[gInfo.Key] = nextNum;
                        usedNumbers.Add(nextNum);
                    }
                }

                // Update GRID only (not Tekla)
                int updatedCount = 0;
                foreach (var gInfo in sortedGroups)
                {
                    int finalNum = groupToNumberMap[gInfo.Key];
                    foreach (string rebarId in gInfo.RebarIds)
                    {
                        var gridItem = Rebars.FirstOrDefault(r => r.Id == rebarId);
                        if (gridItem != null)
                        {
                            gridItem.Seq = finalNum.ToString();
                            updatedCount++;
                        }
                    }
                }

                return $"Preview: {updatedCount} rebars numbered into {sortedGroups.Count} groups. Click APPLY to commit.";
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        /// <summary>Preview Auto V/H prefix: assigns V/H/X prefix to Position based on rebar direction and host type</summary>
        public string PreviewAutoVH(string excludeNames = "")
        {
            if (Rebars.Count == 0) return "No rebars loaded.";
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            var excludeList = new List<string>();
            if (!string.IsNullOrWhiteSpace(excludeNames))
            {
                excludeList = excludeNames.Split(',')
                    .Select(s => s.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            int count = 0;
            int excludedCount = 0;

            foreach (var item in Rebars)
            {
                try
                {
                    if (!int.TryParse(item.Id, out int objId)) continue;

                    // Check exclude list
                    string currentName = (item.Name ?? "").Trim().ToUpper();
                    if (excludeList.Count > 0 && !string.IsNullOrEmpty(currentName))
                    {
                        if (excludeList.Any(ex => currentName.Contains(ex))) { excludedCount++; continue; }
                    }

                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (!(obj is Reinforcement rebar)) continue;

                    ModelObject father = rebar.GetFatherComponent();
                    Part hostPart = father as Part;

                    string prefix = _logicModel.GetAutoPrefix(rebar, hostPart, _slabKeywords, _beamKeywords, _wallKeywords);

                    if (!string.IsNullOrEmpty(prefix) && prefix != item.Position)
                    {
                        item.Position = prefix;
                        count++;
                    }
                }
                catch { }
            }

            string msg = $"Preview: {count} rebars assigned V/H prefix.";
            if (excludedCount > 0) msg += $" Excluded: {excludedCount}.";
            msg += " Click APPLY to commit.";
            return msg;
        }

        /// <summary>Commit all changed + included items to Tekla model</summary>
        public string CommitChangesToTekla()
        {
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            int committed = 0;
            int skipped = 0;

            foreach (var item in Rebars)
            {
                if (!item.IsChanged || !item.IsIncluded) { if (item.IsChanged) skipped++; continue; }

                try
                {
                    if (!int.TryParse(item.Id, out int objId)) continue;
                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (!(obj is Reinforcement rebar)) continue;

                    if (item.Name != item.OriginalName) rebar.Name = item.Name;
                    // Size and Grade are report properties, set via class if possible
                    if (item.Grade != item.OriginalGrade) rebar.Grade = item.Grade;

                    // Apply SEQ number
                    if (item.Seq != item.OriginalSeq)
                    {
                        if (int.TryParse(item.Seq, out int seqNum))
                            rebar.SetUserProperty("REBAR_SEQ_NO", seqNum);
                    }

                    // Apply Position (NumberingSeries Prefix)
                    if (item.Position != item.OriginalPosition)
                    {
                        if (rebar.NumberingSeries != null)
                            rebar.NumberingSeries.Prefix = item.Position;
                    }

                    rebar.Modify();
                    committed++;
                }
                catch { }
            }

            _model.CommitChanges();

            // After commit, update originals so IsChanged resets
            foreach (var item in Rebars)
            {
                if (item.IsIncluded) item.SaveOriginals();
            }

            string msg = $"Committed {committed} changes to Tekla.";
            if (skipped > 0) msg += $" Excluded: {skipped}.";
            return msg;
        }

        /// <summary>Re-read all loaded rebars from Tekla (using stored IDs) to refresh grid</summary>
        public string RefreshFromTekla()
        {
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            var ids = Rebars.Select(r => r.Id).ToList();
            var hostMap = Rebars.ToDictionary(r => r.Id, r => r.HostName);

            Rebars.Clear();

            foreach (string idStr in ids)
            {
                try
                {
                    if (!int.TryParse(idStr, out int objId)) continue;
                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (!(obj is Reinforcement r)) continue;

                    string name = r.Name ?? "";
                    string size = ""; r.GetReportProperty("SIZE", ref size);
                    string grade = ""; r.GetReportProperty("GRADE", ref grade);
                    double length = 0; r.GetReportProperty("LENGTH", ref length);
                    string pos = r.NumberingSeries?.Prefix ?? "";

                    string seq = "";
                    int seqInt = 0;
                    if (r.GetUserProperty("REBAR_SEQ_NO", ref seqInt)) seq = seqInt.ToString();
                    else if (!r.GetUserProperty("REBAR_SEQ_NO", ref seq))
                        r.GetReportProperty("USERDEFINED.REBAR_SEQ_NO", ref seq);

                    int dQty = 0; r.GetReportProperty("NUMBER", ref dQty);
                    string spacing = "---";
                    if (r is RebarGroup group && group.Spacings != null && group.Spacings.Count > 0)
                    {
                        var spacingList = group.Spacings.Cast<double>().Where(s => s > 0).Select(s => Math.Round(s, 0)).ToList();
                        if (spacingList.Count > 0)
                            spacing = spacingList.GroupBy(s => s).OrderByDescending(g => g.Count()).First().Key.ToString();
                    }

                    var item = new RebarInfoItem
                    {
                        Name = name, Size = size, Grade = grade, Length = length,
                        Position = pos, Seq = seq, Quantity = dQty, Id = idStr,
                        TargetSpacing = spacing, HostName = hostMap.ContainsKey(idStr) ? hostMap[idStr] : ""
                    };
                    item.SaveOriginals();
                    Rebars.Add(item);
                }
                catch { }
            }

            return $"Refreshed {Rebars.Count} rebars from Tekla.";
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
                    string pos = r.NumberingSeries?.Prefix ?? "";
                    double length = 0; r.GetReportProperty("LENGTH", ref length);

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

                    var item = new RebarInfoItem
                    {
                        Name = name,
                        Size = size,
                        Grade = grade,
                        Length = length,
                        Position = pos,
                        Seq = seq,
                        Quantity = dQty,
                        Id = id,
                        TargetSpacing = spacing,
                        HostName = rebarToHostMap[id]
                    };
                    item.SaveOriginals();
                    results.Add(item);
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

        /// <summary>
        /// Auto-names all loaded rebars based on host part type and rebar position/orientation.
        /// Rules:
        ///   Wall  → vertical(Z) = "WALL VER.BAR", horizontal = "WALL HOR.BAR"
        ///   Beam  → stirrup(4+ poly pts) = "BEAM STIRRUP", else COG_Z comparison → "TOP BAR_H13" / "BOTTOM BAR_H13"
        ///   Slab  → thickness=75 → "ROOF BOTTOM REBAR", else COG_Z comparison → "SLAB TOP REBAR" / "SLAB BOTTOM REBAR"
        /// </summary>
        public string PreviewAutoName(string excludeNames = "")
        {
            if (!_model.GetConnectionStatus())
                return "Error: Tekla Model not connected.";

            if (Rebars.Count == 0)
                return "No rebars loaded. Pick part first.";

            LoadPersistentSettings();

            var excludeList = new List<string>();
            if (!string.IsNullOrWhiteSpace(excludeNames))
            {
                excludeList = excludeNames.Split(',')
                    .Select(s => s.Trim().ToUpper())
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            int namedCount = 0;
            int skippedCount = 0;
            int excludedCount = 0;

            foreach (var item in Rebars)
            {
                try
                {
                    if (!int.TryParse(item.Id, out int objId)) { skippedCount++; continue; }

                    Tekla.Structures.Identifier identifier = new Tekla.Structures.Identifier(objId);
                    ModelObject obj = _model.SelectModelObject(identifier);
                    if (!(obj is Reinforcement rebar)) { skippedCount++; continue; }

                    // Check exclude list
                    string currentName = (item.Name ?? "").Trim().ToUpper();
                    if (excludeList.Count > 0 && !string.IsNullOrEmpty(currentName))
                    {
                        if (excludeList.Any(ex => currentName.Contains(ex))) { excludedCount++; continue; }
                    }

                    // Determine host type
                    string hostName = (item.HostName ?? "").ToUpper();
                    string partType = "OTHER";
                    if (MatchesKeywords(hostName, _wallKeywords)) partType = "WALL";
                    else if (MatchesKeywords(hostName, _beamKeywords)) partType = "BEAM";
                    else if (MatchesKeywords(hostName, _slabKeywords)) partType = "SLAB";

                    // Fallback: check actual Tekla Part type
                    if (partType == "OTHER")
                    {
                        ModelObject father = rebar.GetFatherComponent();
                        if (father == null)
                        {
                            string fatherType = "";
                            rebar.GetReportProperty("MAIN_PART.OBJECT_TYPE", ref fatherType);
                            fatherType = fatherType.ToUpper();
                            if (fatherType.Contains("BEAM") || fatherType.Contains("COLUMN")) partType = "BEAM";
                            else if (fatherType.Contains("PLATE") || fatherType.Contains("SLAB")) partType = "SLAB";
                            else if (fatherType.Contains("PANEL") || fatherType.Contains("WALL")) partType = "WALL";
                        }
                        else if (father is Beam) partType = "BEAM";
                        else if (father is ContourPlate)
                        {
                            Solid solid = ((Part)father).GetSolid();
                            if (solid != null)
                            {
                                double hZ = solid.MaximumPoint.Z - solid.MinimumPoint.Z;
                                double wXY = Math.Max(solid.MaximumPoint.X - solid.MinimumPoint.X, solid.MaximumPoint.Y - solid.MinimumPoint.Y);
                                partType = (hZ < wXY * 0.3) ? "SLAB" : "WALL";
                            }
                            else partType = "SLAB";
                        }
                        else if (father is Part otherPart)
                        {
                            string fN = (otherPart.Name ?? "").ToUpper();
                            if (fN.Contains("WALL") || fN.Contains("PANEL")) partType = "WALL";
                            else if (fN.Contains("SLAB") || fN.Contains("FLOOR")) partType = "SLAB";
                            else partType = "BEAM";
                        }
                    }

                    if (partType == "OTHER") { skippedCount++; continue; }

                    string newName = "";
                    switch (partType)
                    {
                        case "WALL": newName = DetermineWallRebarName(rebar); break;
                        case "BEAM": newName = DetermineBeamRebarName(rebar); break;
                        case "SLAB": newName = DetermineSlabRebarName(rebar); break;
                    }

                    if (!string.IsNullOrEmpty(newName))
                    {
                        item.Name = newName; // Update GRID only, not Tekla
                        namedCount++;
                    }
                }
                catch { skippedCount++; }
            }

            string msg = $"Preview: {namedCount} rebars named.";
            if (excludedCount > 0) msg += $" Excluded: {excludedCount}.";
            if (skippedCount > 0) msg += $" Skipped: {skippedCount}.";
            msg += " Click APPLY to commit.";
            return msg;
        }

        // ======== Wall: check polygon dominant direction ========
        private string DetermineWallRebarName(Reinforcement rebar)
        {
            Polygon poly = GetFirstPolygon(rebar);
            if (poly == null || poly.Points.Count < 2)
                return "WALL HOR.BAR";

            // Find the longest segment direction in the polygon
            double maxLen = -1;
            Vector bestVec = null;

            for (int i = 0; i < poly.Points.Count - 1; i++)
            {
                var pA = poly.Points[i] as Point;
                var pB = poly.Points[i + 1] as Point;
                if (pA == null || pB == null) continue;

                Vector v = new Vector(pB.X - pA.X, pB.Y - pA.Y, pB.Z - pA.Z);
                double len = v.GetLength();
                if (len > maxLen) { maxLen = len; bestVec = v; }
            }

            if (bestVec == null) return "WALL HOR.BAR";

            double absZ = Math.Abs(bestVec.Z);
            double absXY = Math.Sqrt(bestVec.X * bestVec.X + bestVec.Y * bestVec.Y);

            return absZ > absXY ? "WALL VER.BAR" : "WALL HOR.BAR";
        }

        // ======== Beam: stirrup detection + top/bottom ========
        private string DetermineBeamRebarName(Reinforcement rebar)
        {
            // Build grade+size suffix like "_H13"
            string grade = ""; rebar.GetReportProperty("GRADE", ref grade);
            string size = ""; rebar.GetReportProperty("SIZE", ref size);
            string suffix = $"_{grade}{size}";

            // Stirrup detection: polygon with 4+ points = closed/U-shape
            Polygon poly = GetFirstPolygon(rebar);
            int polyPointCount = (poly != null) ? poly.Points.Count : 0;

            if (polyPointCount >= 4)
                return "BEAM STIRRUP";

            // Longitudinal bar → compare Z position with host center
            double rebarCogZ = 0;
            rebar.GetReportProperty("COG_Z", ref rebarCogZ);

            double hostCogZ = 0;
            rebar.GetReportProperty("MAIN_PART.COG_Z", ref hostCogZ);

            // Trường hợp không lấy được COG từ report property, thử cách khác
            if (hostCogZ == 0)
            {
                ModelObject parent = rebar.GetFatherComponent();
                if (parent is Part hostPart)
                {
                    Solid solid = hostPart.GetSolid();
                    if (solid != null)
                    {
                        hostCogZ = (solid.MaximumPoint.Z + solid.MinimumPoint.Z) / 2.0;
                    }
                }
            }

            return rebarCogZ < hostCogZ
                ? "BOTTOM BAR" + suffix
                : "TOP BAR" + suffix;
        }

        // ======== Slab: roof detection (thickness=75) + top/bottom ========
        private string DetermineSlabRebarName(Reinforcement rebar)
        {
            // Detect slab thickness for roof
            double thickness = 0;
            rebar.GetReportProperty("MAIN_PART.HEIGHT", ref thickness);

            // Nếu không lấy được HEIGHT, thử PROFILE.HEIGHT
            if (thickness == 0)
            {
                rebar.GetReportProperty("MAIN_PART.PROFILE.HEIGHT", ref thickness);
            }

            // Thử lấy trực tiếp từ host part
            if (thickness == 0)
            {
                ModelObject parent = rebar.GetFatherComponent();
                if (parent is Part hostPart)
                {
                    Solid solid = hostPart.GetSolid();
                    if (solid != null)
                    {
                        thickness = solid.MaximumPoint.Z - solid.MinimumPoint.Z;
                    }
                }
            }

            // Sàn mái: chiều dày ≈ 75mm
            if (Math.Abs(thickness - 75) < 5)
                return "ROOF BOTTOM REBAR";

            // So sánh vị trí Z rebar với tâm sàn
            double rebarCogZ = 0;
            rebar.GetReportProperty("COG_Z", ref rebarCogZ);

            double hostCogZ = 0;
            rebar.GetReportProperty("MAIN_PART.COG_Z", ref hostCogZ);

            if (hostCogZ == 0)
            {
                ModelObject parent = rebar.GetFatherComponent();
                if (parent is Part hostPart)
                {
                    Solid solid = hostPart.GetSolid();
                    if (solid != null)
                    {
                        hostCogZ = (solid.MaximumPoint.Z + solid.MinimumPoint.Z) / 2.0;
                    }
                }
            }

            return rebarCogZ >= hostCogZ
                ? "SLAB TOP REBAR"
                : "SLAB BOTTOM REBAR";
        }

        // ======== Utility: get first polygon from any rebar type ========
        private Polygon GetFirstPolygon(Reinforcement rebar)
        {
            if (rebar is RebarGroup group && group.Polygons.Count > 0)
                return group.Polygons[0] as Polygon;
            if (rebar is SingleRebar single)
                return single.Polygon;
            return null;
        }
    }

    public class RebarInfoItem : INotifyPropertyChanged
    {
        // === Backing fields ===
        private string _position, _seq, _name, _size, _grade, _targetSpacing, _hostName, _id;
        private double _length;
        private int _quantity;
        private bool _isIncluded = true;

        // === Original values (from Tekla) ===
        public string OriginalName { get; private set; }
        public string OriginalSeq { get; private set; }
        public string OriginalPosition { get; private set; }
        public string OriginalSize { get; private set; }
        public string OriginalGrade { get; private set; }

        // === Editable properties ===
        public string Position { get => _position; set { if (_position != value) { _position = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string Seq { get => _seq; set { if (_seq != value) { _seq = value; Notify(); Notify(nameof(IsChanged)); Notify(nameof(SeqNum)); } } }
        public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string Size { get => _size; set { if (_size != value) { _size = value; Notify(); Notify(nameof(IsChanged)); Notify(nameof(SizeNum)); } } }
        public string Grade { get => _grade; set { if (_grade != value) { _grade = value; Notify(); Notify(nameof(IsChanged)); } } }

        // === Read-only (from Tekla, not editable) ===
        public double Length { get => _length; set { _length = value; Notify(); } }
        public int Quantity { get => _quantity; set { _quantity = value; Notify(); } }
        public string TargetSpacing { get => _targetSpacing; set { _targetSpacing = value; Notify(); } }
        public string HostName { get => _hostName; set { _hostName = value; Notify(); } }
        public string Id { get => _id; set { _id = value; Notify(); } }

        // === Computed sort-helpers ===
        public int SeqNum => int.TryParse(Seq, out int s) ? s : 0;
        public double SizeNum
        {
            get
            {
                if (string.IsNullOrEmpty(Size)) return 0;
                string cleanSize = new string(Size.Where(c => char.IsDigit(c) || c == '.').ToArray());
                return double.TryParse(cleanSize, out double d) ? d : 0;
            }
        }

        // === Change tracking ===
        public bool IsChanged =>
            Name != OriginalName ||
            Seq != OriginalSeq ||
            Position != OriginalPosition ||
            Size != OriginalSize ||
            Grade != OriginalGrade;

        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; Notify(); }
        }

        /// <summary>Save current values as originals (called after loading from Tekla or after commit)</summary>
        public void SaveOriginals()
        {
            OriginalName = Name;
            OriginalSeq = Seq;
            OriginalPosition = Position;
            OriginalSize = Size;
            OriginalGrade = Grade;
            _isIncluded = true;
            Notify(nameof(IsChanged));
            Notify(nameof(IsIncluded));
        }

        /// <summary>Revert all editable fields to original Tekla values</summary>
        public void RevertToOriginal()
        {
            Name = OriginalName;
            Seq = OriginalSeq;
            Position = OriginalPosition;
            Size = OriginalSize;
            Grade = OriginalGrade;
            IsIncluded = true;
        }

        // === INotifyPropertyChanged ===
        public event PropertyChangedEventHandler PropertyChanged;
        private void Notify([CallerMemberName] string prop = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
    }

    public class RebarNumberingGroupInfo
    {
        public string Key { get; set; }
        public List<Reinforcement> Rebars { get; set; }
        public List<string> RebarIds { get; set; } = new List<string>();
        public int PartTypePriority { get; set; }
        public string HostName { get; set; }
        public int HostId { get; set; }
        public double Length { get; set; }
    }
}

