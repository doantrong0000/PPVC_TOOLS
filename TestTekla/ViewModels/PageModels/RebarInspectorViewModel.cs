using System;
using System.Collections;
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

        private int _startingNumber = 1;
        private string _slabKeywords = "SLAB,FLOOR";
        private string _beamKeywords = "TB,BEAM";
        private string _wallKeywords = "TW,SW,WALL";

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

                LoadSizeColorMapping(settings);

                OnPropertyChanged(nameof(StartingNumber));
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
                var groupSets = rebarMap.GroupBy(kv => GetRebarSignature(kv.Value)).ToList();

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

                    // Check USER_FIELD_4 for "BENDING" flag
                    string userField4 = "";
                    firstRebar.GetUserProperty("USER_FIELD_4", ref userField4);
                    bool isBending = !string.IsNullOrWhiteSpace(userField4) &&
                                     userField4.Trim().Equals("BENDING", StringComparison.OrdinalIgnoreCase);

                    groupSortData.Add(new RebarNumberingGroupInfo
                    {
                        Key = group.Key,
                        Rebars = group.Select(kv => kv.Value).ToList(),
                        RebarIds = group.Select(kv => kv.Key).ToList(),
                        PartTypePriority = typePriority,
                        HostId = hostId,
                        HostName = hostName,
                        Length = length,
                        IsBending = isBending
                    });
                }

                // BENDING groups are sorted to the end so they receive the highest SEQ numbers
                var sortedGroups = groupSortData
                    .OrderBy(g => g.IsBending ? 1 : 0)
                    .ThenBy(g => g.PartTypePriority)
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

                // Assign numbers: non-BENDING unassigned groups first, then BENDING unassigned groups
                // This ensures BENDING groups always receive the highest SEQ numbers
                int nextNum = StartingNumber;
                var unassignedNormal = sortedGroups.Where(g => groupToNumberMap[g.Key] == 0 && !g.IsBending).ToList();
                var unassignedBending = sortedGroups.Where(g => groupToNumberMap[g.Key] == 0 && g.IsBending).ToList();

                foreach (var gInfo in unassignedNormal)
                {
                    while (usedNumbers.Contains(nextNum)) nextNum++;
                    groupToNumberMap[gInfo.Key] = nextNum;
                    usedNumbers.Add(nextNum);
                }
                foreach (var gInfo in unassignedBending)
                {
                    while (usedNumbers.Contains(nextNum)) nextNum++;
                    groupToNumberMap[gInfo.Key] = nextNum;
                    usedNumbers.Add(nextNum);
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

        /// <summary>Check V/H for rebars in walls/columns/slabs and detect rebars outside host</summary>
        public string CheckVH()
        {
            if (Rebars.Count == 0) return "No rebars loaded.";
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            int invalidCount = 0;
            foreach (var item in Rebars)
            {
                item.IsVHInvalid = false; // Reset

                bool isOutsideHost = false;
                string correctPrefix = "";

                try
                {
                    if (int.TryParse(item.Id, out int objId))
                    {
                        var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                        if (obj is Reinforcement rebar)
                        {
                            ModelObject father = rebar.GetFatherComponent();
                            Part hostPart = father as Part;

                            if (hostPart != null)
                            {
                                correctPrefix = GetAutoPrefix(rebar, hostPart, _slabKeywords, _beamKeywords, _wallKeywords);

                                Solid hostSolid = hostPart.GetSolid();
                                Polygon rebarPoly = GetFirstPolygon(rebar);

                                if (hostSolid != null && rebarPoly != null && rebarPoly.Points.Count > 0)
                                {
                                    double minX = double.MaxValue, maxX = double.MinValue;
                                    double minY = double.MaxValue, maxY = double.MinValue;
                                    double minZ = double.MaxValue, maxZ = double.MinValue;

                                    foreach (Tekla.Structures.Geometry3d.Point p in rebarPoly.Points)
                                    {
                                        if (p.X < minX) minX = p.X;
                                        if (p.X > maxX) maxX = p.X;
                                        if (p.Y < minY) minY = p.Y;
                                        if (p.Y > maxY) maxY = p.Y;
                                        if (p.Z < minZ) minZ = p.Z;
                                        if (p.Z > maxZ) maxZ = p.Z;
                                    }

                                    double tol = 150.0; // Tolerance 150mm
                                    if (minX > hostSolid.MaximumPoint.X + tol ||
                                        maxX < hostSolid.MinimumPoint.X - tol ||
                                        minY > hostSolid.MaximumPoint.Y + tol ||
                                        maxY < hostSolid.MinimumPoint.Y - tol ||
                                        minZ > hostSolid.MaximumPoint.Z + tol ||
                                        maxZ < hostSolid.MinimumPoint.Z - tol)
                                    {
                                        isOutsideHost = true;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }

                bool isInvalid = false;

                // If rebar is outside host -> mark as error (red)
                if (isOutsideHost)
                {
                    isInvalid = true;
                }
                else
                {
                    string hostName = (item.HostName ?? "").ToUpper();
                    bool isWallColSlab = MatchesKeywords(hostName, _wallKeywords) ||
                                         MatchesKeywords(hostName, _slabKeywords) ||
                                         hostName.Contains("COLUMN") ||
                                         hostName.Contains("COL");

                    if (isWallColSlab)
                    {
                        string pos = (item.Position ?? "").Trim().ToUpper();

                        // If prefix is X or unknown character (not V, H)
                        if (pos != "V" && pos != "H")
                        {
                            isInvalid = true;
                        }
                        // Or if V/H is assigned but doesn't match actual geometric direction
                        else if (!string.IsNullOrEmpty(correctPrefix) && correctPrefix != "X" && pos != correctPrefix)
                        {
                            isInvalid = true;
                        }
                    }
                }

                if (isInvalid)
                {
                    item.IsVHInvalid = true;
                    invalidCount++;
                }
            }

            return $"Check complete: {invalidCount} rebars with wrong V/H or outside host.";
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

                    // Ensure all properties (like Cover Thickness) are fully loaded before modifying
                    rebar.Select();

                    bool needsModify = false;

                    if (item.Seq != item.OriginalSeq)
                    {
                        if (int.TryParse(item.Seq, out int seqNum))
                            rebar.SetUserProperty("REBAR_SEQ_NO", seqNum);
                    }

                    // Apply Class
                    if (item.ClassStr != item.OriginalClassStr)
                    {
                        if (int.TryParse(item.ClassStr, out int cls))
                        {
                            rebar.Class = cls;
                            needsModify = true;
                        }
                    }


                    if (needsModify)
                    {
                        rebar.Modify();
                    }
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

                    string radiusVal = "";
                    if (r.RadiusValues != null && r.RadiusValues.Count > 0) radiusVal = r.RadiusValues[0].ToString();

                    var item = new RebarInfoItem
                    {
                        Name = name,
                        Size = size,
                        Grade = grade,
                        Length = length,
                        Position = pos,
                        Seq = seq,
                        Quantity = dQty,
                        Id = idStr,
                        TargetSpacing = spacing,
                        HostName = hostMap.ContainsKey(idStr) ? hostMap[idStr] : "",
                        ClassStr = r.Class.ToString(),
                        RadiusStr = radiusVal
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

                // 1. Get rebars from Parts first to ensure correct host association
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

                // 2. Process individually selected Reinforcement objects not yet in the list
                foreach (var obj in targetObjects)
                {
                    if (obj is Reinforcement rebar)
                    {
                        string id = rebar.Identifier.ID.ToString();
                        if (!uniqueRebars.ContainsKey(id))
                        {
                            uniqueRebars[id] = rebar;

                            string hostName = "";

                            // Try to get via direct parent (Father) in Tekla tree structure
                            ModelObject parent = rebar.GetFatherComponent();
                            if (parent is Part p)
                            {
                                hostName = string.IsNullOrEmpty(p.Name) ? p.Profile.ProfileString : p.Name;
                            }

                            // If GetFatherComponent fails, use Report Property as fallback
                            if (string.IsNullOrEmpty(hostName))
                            {
                                rebar.GetReportProperty("MAIN_PART.NAME", ref hostName);
                            }

                            if (string.IsNullOrEmpty(hostName))
                            {
                                rebar.GetReportProperty("FATHER.NAME", ref hostName);
                            }

                            rebarToHostMap[id] = string.IsNullOrEmpty(hostName) ? "No host part" : hostName;
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

                    // Rebar Sequence Number (REBAR_SEQ_NO) is typically an integer UDA
                    string seq = "";
                    int seqInt = 0;
                    if (r.GetUserProperty("REBAR_SEQ_NO", ref seqInt))
                    {
                        seq = seqInt.ToString();
                    }
                    else if (!r.GetUserProperty("REBAR_SEQ_NO", ref seq))
                    {
                        // Fallback via Report Property
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

                    string radiusVal = "";
                    if (r.RadiusValues != null && r.RadiusValues.Count > 0) radiusVal = r.RadiusValues[0].ToString();

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
                        HostName = rebarToHostMap[id],
                        ClassStr = r.Class.ToString(),
                        RadiusStr = radiusVal
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

        // ======== Utility: get first polygon from any rebar type ========
        private Polygon GetFirstPolygon(Reinforcement rebar)
        {
            if (rebar is RebarGroup group && group.Polygons.Count > 0)
                return group.Polygons[0] as Polygon;
            if (rebar is SingleRebar single)
                return single.Polygon;
            return null;
        }

        // ==============================================================================
        // AUTOMATIC COLOR ASSIGNMENT
        // ==============================================================================
        public ObservableCollection<SizeColorItem> SizeColorTable { get; set; }

        public void UpdateSettings(AppSettings settings)
        {
            SettingsService.SaveSettings(settings);
            LoadPersistentSettings(); // Reload keywords
            LoadSizeColorMapping(settings);
            OnPropertyChanged(nameof(SizeColorTable));
        }

        private void LoadSizeColorMapping(AppSettings settings)
        {
            SizeColorTable = new ObservableCollection<SizeColorItem>();
            if (!string.IsNullOrEmpty(settings.SizeClassMapping))
            {
                var parts = settings.SizeClassMapping.Split(';');
                foreach (var p in parts)
                {
                    var pair = p.Split(':');
                    if (pair.Length == 2 && int.TryParse(pair[0], out int sz) && int.TryParse(pair[1], out int cl))
                    {
                        SizeColorTable.Add(new SizeColorItem { RebarSize = sz, RebarClass = cl });
                    }
                }
            }
        }

        public string PreviewAutoColor()
        {
            if (Rebars.Count == 0) return "No rebars loaded.";

            var settings = SettingsService.LoadSettings();
            LoadSizeColorMapping(settings);

            if (SizeColorTable == null || SizeColorTable.Count == 0) return "No mapping rules defined in Settings.";

            int count = 0;
            foreach (var item in Rebars)
            {
                int sz = (int)Math.Round(item.SizeNum);
                var match = SizeColorTable.FirstOrDefault(x => x.RebarSize == sz);
                if (match != null)
                {
                    string targetClass = match.RebarClass.ToString();
                    if (item.ClassStr != targetClass)
                    {
                        item.ClassStr = targetClass;
                        count++;
                    }
                }
            }

            return $"Preview: {count} rebars assigned Color Class based on rule. Click APPLY to commit.";
        }

        private double GetManualBendingRadius(double sizeNum)
        {
            int size = (int)Math.Round(sizeNum);
            switch (size)
            {
                case 6: return 12.00;
                case 8: return 16.00;
                case 10: return 20.00;
                case 12: return 24.00;
                case 13: return 24.00;
                case 14: return 28.00;
                case 16: return 32.00;
                case 18: return 63.00; // 3.5 * size
                case 20: return 70.00;
                case 22: return 80.00;
                case 25: return 87.00;
                case 28: return 100.00;
                case 32: return 112.00;
                case 40: return 140.00;
                case 50: return 175.00;
                default:
                    if (size <= 16) return size * 2.0;
                    return size * 3.5;
            }
        }

        public string PreviewAutoRadius()
        {
            if (Rebars.Count == 0) return "No rebars loaded.";
            int count = 0;

            foreach (var item in Rebars)
            {
                if (item.SizeNum > 0)
                {
                    double targetRadius = GetManualBendingRadius(item.SizeNum);

                    if (targetRadius > 0)
                    {
                        string newRadiusStr = targetRadius.ToString("0.##");
                        if (item.RadiusStr != newRadiusStr)
                        {
                            item.RadiusStr = newRadiusStr;
                            count++;
                        }
                    }
                }
            }

            return $"Preview: {count} rebars assigned manual bending radius. Click APPLY to commit.";
        }

        // ==============================================================================
        // OVERLAP DETECTION
        // ==============================================================================

        /// <summary>
        /// Returns a shape-only key (SIZE + SHAPE + HOOK), WITHOUT length,
        /// used for fuzzy overlap grouping.
        /// </summary>
        private string GetOverlapShapeKey(Reinforcement rebar)
        {
            string size = ""; rebar.GetReportProperty("SIZE", ref size);
            string grade = ""; rebar.GetReportProperty("GRADE", ref grade);
            string name = rebar.Name;
            string prefix = rebar.NumberingSeries?.Prefix ?? "";

            string shapeKey = "";
            rebar.GetReportProperty("SHAPE", ref shapeKey);

            double length = 0;
            rebar.GetReportProperty("LENGTH", ref length);
            double roundedLength = Math.Round(length / 5.0) * 5.0;

            string rebarPos = "";
            rebar.GetReportProperty("REBAR_POS", ref rebarPos);

            return $"{name}|{prefix}|{grade}|{size}|{shapeKey}|{roundedLength}|{rebarPos}";
        }

        public string FindDuplicates()
        {
            if (Rebars.Count == 0) return "No rebars loaded. Pick part first.";
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            try
            {
                // Reset all check again flags
                foreach (var item in Rebars) item.IsCheckAgain = false;

                // Build shape keys + lengths for each rebar to compare
                var rebarShapeKeys = new Dictionary<string, string>();
                var rebarLengths = new Dictionary<string, double>();
                var rebarMarks = new Dictionary<string, string>();
                foreach (var item in Rebars)
                {
                    if (!int.TryParse(item.Id, out int objId)) continue;
                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (obj is Reinforcement r)
                    {
                        rebarShapeKeys[item.Id] = GetOverlapShapeKey(r);
                        double len = 0; r.GetReportProperty("LENGTH", ref len);
                        rebarLengths[item.Id] = len;
                        string mark = ""; r.GetReportProperty("REBAR_POS", ref mark);
                        rebarMarks[item.Id] = mark;
                    }
                }

                int duplicateCount = 0;
                var duplicateItems = new HashSet<RebarInfoItem>();

                for (int i = 0; i < Rebars.Count; i++)
                {
                    bool hasConflict = false;
                    for (int j = i + 1; j < Rebars.Count; j++)
                    {
                        var a = Rebars[i];
                        var b = Rebars[j];

                        string seqA = (a.Seq ?? "").Trim();
                        string seqB = (b.Seq ?? "").Trim();

                        if (string.IsNullOrEmpty(seqA) || seqA == "0" || seqA == "0.001") continue;
                        if (string.IsNullOrEmpty(seqB) || seqB == "0" || seqB == "0.001") continue;

                        if (seqA == seqB)
                        {
                            // If SEQ is the same, verify Shape, Length, Mark
                            string keyA = rebarShapeKeys.ContainsKey(a.Id) ? rebarShapeKeys[a.Id] : a.Id;
                            string keyB = rebarShapeKeys.ContainsKey(b.Id) ? rebarShapeKeys[b.Id] : b.Id;

                            // GetOverlapShapeKey includes shape, mark, length, so if keys differ, there's a conflict
                            if (keyA != keyB)
                            {
                                hasConflict = true;
                                duplicateItems.Add(a);
                                duplicateItems.Add(b);
                            }
                        }
                    }
                }

                if (duplicateItems.Count > 0)
                {
                    foreach (var item in duplicateItems) item.IsCheckAgain = true;
                    var sortedList = Rebars.OrderByDescending(r => r.IsCheckAgain).ThenBy(r => r.SeqNum).ToList();
                    Rebars.Clear();
                    foreach (var item in sortedList) Rebars.Add(item);
                    return $"Found {duplicateItems.Count} rebars with conflicting SEQ (Check again).";
                }
                else
                {
                    return "No SEQ conflicts found.";
                }
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        /// <summary>
        /// Finds rebars that share the same shape signature and have LENGTH difference
        /// within the configured tolerance, but different SEQ numbers (overlap).
        /// Reorders the Rebars collection so overlap pairs are placed at the top, side by side.
        /// </summary>
        public string FindOverlaps()
        {
            if (Rebars.Count == 0) return "No rebars loaded. Pick part first.";
            if (!_model.GetConnectionStatus()) return "Error: Tekla not connected.";

            try
            {
                // Load tolerance from settings
                var settings = SettingsService.LoadSettings();
                double tolerance = settings.OverlapLengthTolerance;

                // Reset all overlap and check again flags
                foreach (var item in Rebars) { item.IsOverlap = false; item.IsCheckAgain = false; }

                // Build shape keys + lengths for each rebar
                var rebarShapeKeys = new Dictionary<string, string>();   // id -> shape key (no length)
                var rebarLengths = new Dictionary<string, double>();     // id -> LENGTH
                foreach (var item in Rebars)
                {
                    if (!int.TryParse(item.Id, out int objId)) continue;
                    var obj = _model.SelectModelObject(new Tekla.Structures.Identifier(objId));
                    if (obj is Reinforcement r)
                    {
                        rebarShapeKeys[item.Id] = GetOverlapShapeKey(r);
                        double len = 0;
                        r.GetReportProperty("LENGTH", ref len);
                        rebarLengths[item.Id] = len;
                    }
                }

                // Group by shape key (exact match on SIZE + SHAPE + HOOK, ignoring LENGTH)
                var shapeGroups = Rebars
                    .Where(r => rebarShapeKeys.ContainsKey(r.Id))
                    .GroupBy(r => rebarShapeKeys[r.Id])
                    .ToList();

                // Find overlap groups using fuzzy length comparison
                int overlapCount = 0;
                var overlapItems = new List<RebarInfoItem>();
                var nonOverlapItems = new List<RebarInfoItem>();

                foreach (var group in shapeGroups)
                {
                    var items = group.ToList();

                    // Within this shape group, find pairs with different SEQ but similar length
                    var overlapSet = new HashSet<RebarInfoItem>();

                    for (int i = 0; i < items.Count; i++)
                    {
                        for (int j = i + 1; j < items.Count; j++)
                        {
                            var a = items[i];
                            var b = items[j];

                            string seqA = (a.Seq ?? "").Trim();
                            string seqB = (b.Seq ?? "").Trim();

                            // Both must have valid SEQ and they must differ
                            if (string.IsNullOrEmpty(seqA) || seqA == "0") continue;
                            if (string.IsNullOrEmpty(seqB) || seqB == "0") continue;
                            if (seqA == seqB) continue;

                            // Check length tolerance
                            double lenA = rebarLengths.ContainsKey(a.Id) ? rebarLengths[a.Id] : 0;
                            double lenB = rebarLengths.ContainsKey(b.Id) ? rebarLengths[b.Id] : 0;

                            if (Math.Abs(lenA - lenB) <= tolerance)
                            {
                                overlapSet.Add(a);
                                overlapSet.Add(b);
                            }
                        }
                    }

                    if (overlapSet.Count > 0)
                    {
                        foreach (var item in overlapSet) item.IsOverlap = true;
                        overlapItems.AddRange(overlapSet.OrderBy(r => r.SeqNum));
                        overlapCount += overlapSet.Count;
                        nonOverlapItems.AddRange(items.Where(r => !overlapSet.Contains(r)));
                    }
                    else
                    {
                        nonOverlapItems.AddRange(items);
                    }
                }

                // Also add items without signatures to non-overlap
                var itemsWithoutSig = Rebars.Where(r => !rebarShapeKeys.ContainsKey(r.Id)).ToList();
                nonOverlapItems.AddRange(itemsWithoutSig);

                // Rebuild Rebars collection: overlaps first, then the rest
                Rebars.Clear();
                foreach (var item in overlapItems) Rebars.Add(item);
                foreach (var item in nonOverlapItems) Rebars.Add(item);

                if (overlapCount > 0)
                    return $"Found {overlapCount} overlap rebars (tolerance: {tolerance}mm). Moved to top.";
                else
                    return $"No overlaps found (tolerance: {tolerance}mm). All rebars have unique shapes or consistent SEQ.";
            }
            catch (Exception ex) { return "Error: " + ex.Message; }
        }

        // ==============================================================================
        // REBAR AUTO V/H DIRECTION LOGIC (Merged from RebarNumberingModel)
        // ==============================================================================
        public string GetAutoPrefix(Reinforcement rebar, Part hostPart, string slabKeys = "SLAB,FLOOR", string beamKeys = "TB,BEAM", string wallKeys = "TW,SW,WALL")
        {
            string prefix = "";
            if (hostPart != null)
            {
                string hostName = (hostPart.Name ?? "").ToUpper();
                bool isWall = MatchesKeywords(hostName, wallKeys);
                bool isBeam = MatchesKeywords(hostName, beamKeys);
                bool isSlab = MatchesKeywords(hostName, slabKeys);

                if (!isWall && !isBeam && !isSlab)
                    if (hostPart is ContourPlate) isSlab = true;

                if (isSlab) prefix = GetRebarDirectionPrefix(rebar, false);
                else prefix = GetPartDirectionPrefix(hostPart);
            }
            else
            {
                prefix = GetRebarDirectionPrefix(rebar, false);
            }
            return prefix;
        }

        private string GetDirectionFromVector(Vector vec, bool isWall = false)
        {
            vec.Normalize();
            double absX = Math.Abs(vec.X);
            double absY = Math.Abs(vec.Y);
            double absZ = Math.Abs(vec.Z);
            if (isWall)
            {
                if (absZ > 0.98) return "V";
                if (absX > 0.98 || absY > 0.98) return "H";
            }
            else
            {
                if (absX > 0.98) return "H";
                if (absY > 0.98) return "V";
            }
            return "X";
        }

        private string GetRebarDirectionPrefix(Reinforcement rebar, bool isWall)
        {
            Polygon poly = GetFirstPolygon(rebar);
            if (poly == null || poly.Points.Count < 2) return "";
            double maxLength = -1;
            Vector bestVec = null;
            for (int i = 0; i < poly.Points.Count - 1; i++)
            {
                if (poly.Points[i] is Point pA && poly.Points[i + 1] is Point pB)
                {
                    Vector currentVec = new Vector(pB.X - pA.X, pB.Y - pA.Y, pB.Z - pA.Z);
                    double len = currentVec.GetLength();
                    if (len > maxLength) { maxLength = len; bestVec = currentVec; }
                }
            }
            if (bestVec == null || maxLength < 0.1) return "";
            return GetDirectionFromVector(bestVec, isWall);
        }

        private string GetPartDirectionPrefix(Part part)
        {
            Vector vec = null;
            if (part is Beam beam)
            {
                vec = new Vector(beam.EndPoint.X - beam.StartPoint.X, beam.EndPoint.Y - beam.StartPoint.Y, 0);
            }
            else if (part is ContourPlate cp)
            {
                double maxLen = -1;
                Vector bestVec = null;
                var points = cp.Contour.ContourPoints;
                if (points != null && points.Count > 1)
                {
                    for (int i = 0; i < points.Count; i++)
                    {
                        var p1 = points[i] as ContourPoint;
                        var p2 = points[(i + 1) % points.Count] as ContourPoint;
                        if (p1 != null && p2 != null)
                        {
                            Vector v = new Vector(p2.X - p1.X, p2.Y - p1.Y, 0);
                            double len = v.GetLength();
                            if (len > maxLen) { maxLen = len; bestVec = v; }
                        }
                    }
                }
                vec = bestVec ?? part.GetCoordinateSystem().AxisX;
            }
            else
            {
                vec = part.GetCoordinateSystem().AxisX;
            }
            if (vec == null || vec.GetLength() < 0.1) return "";
            return GetDirectionFromVector(vec);
        }

        public string GetRebarSignature(Reinforcement rebar)
        {
            string size = "";
            rebar.GetReportProperty("SIZE", ref size);

            double length = 0;
            rebar.GetReportProperty("LENGTH", ref length);

            string shapeKey = "";
            rebar.GetReportProperty("SHAPE", ref shapeKey);

            return $"{size}|{length}|{shapeKey}";
        }


    }

    public class RebarInfoItem : INotifyPropertyChanged
    {
        // === Backing fields ===
        private string _position, _seq, _name, _size, _grade, _classStr, _targetSpacing, _hostName, _id, _radiusStr;
        private double _length;
        private int _quantity;
        private bool _isIncluded = true;
        private bool _isOverlap = false;
        private bool _isCheckAgain = false;
        private bool _isVHInvalid = false;

        // === Original values (from Tekla) ===
        public string OriginalName { get; private set; }
        public string OriginalSeq { get; private set; }
        public string OriginalPosition { get; private set; }
        public string OriginalSize { get; private set; }
        public string OriginalGrade { get; private set; }
        public string OriginalClassStr { get; private set; }
        public string OriginalRadiusStr { get; private set; }

        // === Editable properties ===
        public string Position { get => _position; set { if (_position != value) { _position = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string Seq { get => _seq; set { if (_seq != value) { _seq = value; Notify(); Notify(nameof(IsChanged)); Notify(nameof(SeqNum)); } } }
        public string Name { get => _name; set { if (_name != value) { _name = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string Size { get => _size; set { if (_size != value) { _size = value; Notify(); Notify(nameof(IsChanged)); Notify(nameof(SizeNum)); } } }
        public string Grade { get => _grade; set { if (_grade != value) { _grade = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string ClassStr { get => _classStr; set { if (_classStr != value) { _classStr = value; Notify(); Notify(nameof(IsChanged)); } } }
        public string RadiusStr { get => _radiusStr; set { if (_radiusStr != value) { _radiusStr = value; Notify(); Notify(nameof(IsChanged)); } } }

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
            Grade != OriginalGrade ||
            ClassStr != OriginalClassStr ||
            RadiusStr != OriginalRadiusStr;

        public bool IsIncluded
        {
            get => _isIncluded;
            set { _isIncluded = value; Notify(); }
        }

        public bool IsOverlap
        {
            get => _isOverlap;
            set { _isOverlap = value; Notify(); }
        }

        public bool IsCheckAgain
        {
            get => _isCheckAgain;
            set { _isCheckAgain = value; Notify(); }
        }

        public bool IsVHInvalid
        {
            get => _isVHInvalid;
            set { _isVHInvalid = value; Notify(); }
        }

        /// <summary>Save current values as originals (called after loading from Tekla or after commit)</summary>
        public void SaveOriginals()
        {
            OriginalName = Name;
            OriginalSeq = Seq;
            OriginalPosition = Position;
            OriginalSize = Size;
            OriginalGrade = Grade;
            OriginalClassStr = ClassStr;
            OriginalRadiusStr = RadiusStr;
            _isIncluded = true;
            Notify(nameof(IsChanged));
            Notify(nameof(IsIncluded));
        }

        public void RevertToOriginal()
        {
            Name = OriginalName;
            Seq = OriginalSeq;
            Position = OriginalPosition;
            Size = OriginalSize;
            Grade = OriginalGrade;
            ClassStr = OriginalClassStr;
            RadiusStr = OriginalRadiusStr;
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
        /// <summary>True if USER_FIELD_4 == "BENDING" — these groups are numbered last (highest SEQ)</summary>
        public bool IsBending { get; set; }
    }

    public class SizeColorItem
    {
        public int RebarSize { get; set; }
        public int RebarClass { get; set; }
    }
}

