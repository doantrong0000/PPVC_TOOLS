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

        /// <summary>Preview numbering: calculates SEQ numbers and updates the grid only (no Tekla write)</summary>
        public string PreviewNumbering(bool reassignAll = false)
        {
            if (Rebars.Count == 0)
                return "No rebars loaded. Pick part first.";

            if (!_model.GetConnectionStatus())
                return "Error: Tekla not connected.";

            try
            {
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
                        HostId = hostId,
                        HostName = hostName,
                        Length = length,
                        IsBending = isBending
                    });
                }

                // BENDING groups are sorted to the end so they receive the highest SEQ numbers
                var sortedGroups = groupSortData
                    .OrderBy(g => g.IsBending ? 1 : 0)
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

