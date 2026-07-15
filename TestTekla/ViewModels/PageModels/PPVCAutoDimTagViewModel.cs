using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.IO;
using System.Runtime.CompilerServices;
using System.Security.Policy;
using System.Windows;
using Tekla.Structures.Drawing;
using TestTekla.ViewModels;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
using TS = Tekla.Structures;
// Alias cho Tekla Drawing API
using TSD = Tekla.Structures.Drawing;
using TSDUI = Tekla.Structures.Drawing.UI;
using TSM = Tekla.Structures.Model;
using TSMO = Tekla.Structures.Model.Operations;

namespace TeklaApp.ViewModels.PageModels
{
    public class DimMappingRule : BaseViewModel
    {
        private string _rebarName;
        public string RebarName
        {
            get { return _rebarName; }
            set { _rebarName = value; OnPropertyChanged(); }
        }

        private string _prefix;
        public string Prefix
        {
            get { return _prefix; }
            set { _prefix = value; OnPropertyChanged(); }
        }

        private string _dimProperty;
        public string DimProperty
        {
            get { return _dimProperty; }
            set { _dimProperty = value; OnPropertyChanged(); }
        }

    }

    public class TagMappingRule : INotifyPropertyChanged
    {
        private string _rebarName;
        public string RebarName
        {
            get { return _rebarName; }
            set { _rebarName = value; OnPropertyChanged(); }
        }

        private string _prefix;
        public string Prefix
        {
            get { return _prefix; }
            set { _prefix = value; OnPropertyChanged(); }
        }

        private string _tagProperty;
        public string TagProperty
        {
            get { return _tagProperty; }
            set { _tagProperty = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    public class PPVCAutoDimTagViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DimMappingRule> DimMappingRules { get; set; }
        public ObservableCollection<string> AvailableDimProperties { get; set; }
        public ObservableCollection<string> AvailableProfiles { get; set; }

        // Tag properties
        public ObservableCollection<TagMappingRule> TagMappingRules { get; set; }
        public ObservableCollection<string> AvailableTagProperties { get; set; }
        public ObservableCollection<string> AvailableTagProfiles { get; set; }

        private string _selectedProfile;
        public string SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                _selectedProfile = value;
                OnPropertyChanged();
                LoadProfileRules(value);
            }
        }

        private string _selectedTagProfile;
        public string SelectedTagProfile
        {
            get => _selectedTagProfile;
            set
            {
                _selectedTagProfile = value;
                OnPropertyChanged();
                LoadTagProfileRules(value);
            }
        }

        private Dictionary<string, List<DimMappingRule>> _allProfiles;
        private Dictionary<string, List<TagMappingRule>> _allTagProfiles;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public PPVCAutoDimTagViewModel()
        {
            AvailableDimProperties = new ObservableCollection<string>();
            AvailableProfiles = new ObservableCollection<string>();
            DimMappingRules = new ObservableCollection<DimMappingRule>();
            _allProfiles = new Dictionary<string, List<DimMappingRule>>();

            TagMappingRules = new ObservableCollection<TagMappingRule>();
            AvailableTagProperties = new ObservableCollection<string>();
            AvailableTagProfiles = new ObservableCollection<string>();
            _allTagProfiles = new Dictionary<string, List<TagMappingRule>>();

            LoadDimProperties();
            LoadDimMappingRules();
            LoadTagProperties();
            LoadTagMappingRules();
        }

        private void LoadDimProperties()
        {
            AvailableDimProperties = new ObservableCollection<string>();
            try
            {
                TSM.Model myModel = new TSM.Model();
                if (myModel.GetConnectionStatus())
                {
                    string modelPath = myModel.GetInfo().ModelPath;
                    string attributesPath = Path.Combine(modelPath, "attributes");

                    if (Directory.Exists(attributesPath))
                    {
                        string[] files = Directory.GetFiles(attributesPath, "*.rdim");
                        foreach (string file in files)
                        {
                            AvailableDimProperties.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
            }
            catch { }

            if (AvailableDimProperties.Count == 0)
            {
                AvailableDimProperties.Add("standard");
            }
        }

        private void LoadTagProperties()
        {
            AvailableTagProperties = new ObservableCollection<string>();
            try
            {
                TSM.Model myModel = new TSM.Model();
                if (myModel.GetConnectionStatus())
                {
                    string modelPath = myModel.GetInfo().ModelPath;
                    string attributesPath = Path.Combine(modelPath, "attributes");

                    if (Directory.Exists(attributesPath))
                    {
                        // Tìm các file .RM (Rebar Mark attributes) trong thư mục attributes
                        string[] files = Directory.GetFiles(attributesPath, "*.RM");
                        foreach (string file in files)
                        {
                            AvailableTagProperties.Add(Path.GetFileNameWithoutExtension(file));
                        }
                    }
                }
            }
            catch { }

            if (AvailableTagProperties.Count == 0)
            {
                AvailableTagProperties.Add("standard");
            }
        }

        private string GetMappingRulesJsonPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appData, "TeklaTools", "Json");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath, "DimMappingRules.json");
        }

        private string GetTagMappingRulesJsonPath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appData, "TeklaTools", "Json");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath, "TagMappingRules.json");
        }

        private void LoadDimMappingRules()
        {
            string jsonFilePath = GetMappingRulesJsonPath();

            if (File.Exists(jsonFilePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonFilePath);
                    _allProfiles = JsonConvert.DeserializeObject<Dictionary<string, List<DimMappingRule>>>(jsonContent);
                }
                catch { } // Fallback if error or old format
            }

            if (_allProfiles == null || _allProfiles.Count == 0)
            {
                _allProfiles = new Dictionary<string, List<DimMappingRule>>();
                _allProfiles["standard"] = new List<DimMappingRule>
                {
                new DimMappingRule { RebarName = "STIRRUP", Prefix = "V", DimProperty = "A1" },
                new DimMappingRule { RebarName = "MAIN BAR", Prefix = "V", DimProperty = "A2" }
                };
                SaveAllProfiles();
            }

            AvailableProfiles.Clear();
            foreach (var profile in _allProfiles.Keys)
            {
                AvailableProfiles.Add(profile);
            }

            SelectedProfile = AvailableProfiles.Contains("standard") ? "standard" : AvailableProfiles[0];
        }

        private void LoadTagMappingRules()
        {
            string jsonFilePath = GetTagMappingRulesJsonPath();

            if (File.Exists(jsonFilePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonFilePath);
                    _allTagProfiles = JsonConvert.DeserializeObject<Dictionary<string, List<TagMappingRule>>>(jsonContent);
                }
                catch { }
            }

            if (_allTagProfiles == null || _allTagProfiles.Count == 0)
            {
                _allTagProfiles = new Dictionary<string, List<TagMappingRule>>();
                _allTagProfiles["standard"] = new List<TagMappingRule>
                {
                    new TagMappingRule { RebarName = "STIRRUP", Prefix = "V", TagProperty = "standard" },
                    new TagMappingRule { RebarName = "MAIN BAR", Prefix = "V", TagProperty = "standard" }
                };
                SaveAllTagProfiles();
            }

            AvailableTagProfiles.Clear();
            foreach (var profile in _allTagProfiles.Keys)
            {
                AvailableTagProfiles.Add(profile);
            }

            SelectedTagProfile = AvailableTagProfiles.Contains("standard") ? "standard" : AvailableTagProfiles[0];
        }

        private void LoadProfileRules(string profileName)
        {
            if (string.IsNullOrEmpty(profileName) || _allProfiles == null) return;

            DimMappingRules.Clear();
            if (_allProfiles.TryGetValue(profileName, out var rules))
            {
                foreach (var r in rules)
                {
                    DimMappingRules.Add(new DimMappingRule { RebarName = r.RebarName, Prefix = r.Prefix, DimProperty = r.DimProperty });
                }
            }
        }

        private void LoadTagProfileRules(string profileName)
        {
            if (string.IsNullOrEmpty(profileName) || _allTagProfiles == null) return;

            TagMappingRules.Clear();
            if (_allTagProfiles.TryGetValue(profileName, out var rules))
            {
                foreach (var r in rules)
                {
                    TagMappingRules.Add(new TagMappingRule { RebarName = r.RebarName, Prefix = r.Prefix, TagProperty = r.TagProperty });
                }
            }
        }

        private void SaveAllProfiles()
        {
            try
            {
                string jsonFilePath = GetMappingRulesJsonPath();
                string json = JsonConvert.SerializeObject(_allProfiles, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json);
            }
            catch { }
        }

        private void SaveAllTagProfiles()
        {
            try
            {
                string jsonFilePath = GetTagMappingRulesJsonPath();
                string json = JsonConvert.SerializeObject(_allTagProfiles, Formatting.Indented);
                File.WriteAllText(jsonFilePath, json);
            }
            catch { }
        }

        public void SaveCurrentProfile(bool showSuccessMsg = true)
        {
            if (string.IsNullOrEmpty(SelectedProfile)) return;
            _allProfiles[SelectedProfile] = new List<DimMappingRule>(DimMappingRules);
            SaveAllProfiles();
            if (showSuccessMsg)
            {
                MessageBox.Show($"Profile '{SelectedProfile}' saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void SaveAsProfile(string newProfileName)
        {
            _allProfiles[newProfileName] = new List<DimMappingRule>(DimMappingRules);
            if (!AvailableProfiles.Contains(newProfileName))
            {
                AvailableProfiles.Add(newProfileName);
            }
            SelectedProfile = newProfileName;
            SaveAllProfiles();
            MessageBox.Show($"Profile saved as '{newProfileName}'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void DeleteCurrentProfile()
        {
            if (string.IsNullOrEmpty(SelectedProfile)) return;
            if (SelectedProfile.Equals("standard", StringComparison.OrdinalIgnoreCase)) return;

            string profileToDelete = SelectedProfile;
            _allProfiles.Remove(profileToDelete);

            SaveAllProfiles();

            AvailableProfiles.Clear();
            DimMappingRules.Clear();
            SelectedProfile = null;

            LoadDimMappingRules();

            MessageBox.Show($"Profile '{profileToDelete}' has been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void SaveCurrentTagProfile(bool showSuccessMsg = true)
        {
            if (string.IsNullOrEmpty(SelectedTagProfile)) return;
            _allTagProfiles[SelectedTagProfile] = new List<TagMappingRule>(TagMappingRules);
            SaveAllTagProfiles();
            if (showSuccessMsg)
            {
                MessageBox.Show($"Tag Profile '{SelectedTagProfile}' saved.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        public void SaveAsTagProfile(string newProfileName)
        {
            _allTagProfiles[newProfileName] = new List<TagMappingRule>(TagMappingRules);
            if (!AvailableTagProfiles.Contains(newProfileName))
            {
                AvailableTagProfiles.Add(newProfileName);
            }
            SelectedTagProfile = newProfileName;
            SaveAllTagProfiles();
            MessageBox.Show($"Tag Profile saved as '{newProfileName}'.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        public void DeleteCurrentTagProfile()
        {
            if (string.IsNullOrEmpty(SelectedTagProfile)) return;
            if (SelectedTagProfile.Equals("standard", StringComparison.OrdinalIgnoreCase)) return;

            string profileToDelete = SelectedTagProfile;
            _allTagProfiles.Remove(profileToDelete);

            SaveAllTagProfiles();

            AvailableTagProfiles.Clear();
            TagMappingRules.Clear();
            SelectedTagProfile = null;

            LoadTagMappingRules();

            MessageBox.Show($"Tag Profile '{profileToDelete}' has been deleted.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #region Rebar Dimension

        // 1. ADD 'async' KEYWORD HERE
        /// <summary>
        /// Automatically create Dimensions for all reinforcements in the selected View
        /// </summary>
        public void AutoDimSelectedView()
        {
            try
            {
                SaveCurrentProfile(false);

                TSD.DrawingHandler dh = new TSD.DrawingHandler();
                TSD.Drawing activeDrawing = dh.GetActiveDrawing();
                TSM.Model myModel = new TSM.Model();

                if (activeDrawing == null)
                {
                    MessageBox.Show("Please open a drawing first!");
                    return;
                }

                TSDUI.DrawingObjectSelector objectSelector = dh.GetDrawingObjectSelector();
                ArrayList selectedViews = new ArrayList();
                TSD.DrawingObjectEnumerator selectedObjects = objectSelector.GetSelected();

                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is TSD.View view)
                    {
                        selectedViews.Add(view);
                    }
                }

                if (selectedViews.Count > 0)
                {
                    TSD.View activeView = selectedViews[0] as TSD.View;

                    // 1. TẠO "POOL" CHỨA TẤT CẢ THÉP (Chỉ quét bản vẽ 1 lần duy nhất)
                    List<TSD.ReinforcementGroup> unassignedRebars = new List<TSD.ReinforcementGroup>();
                    TSD.DrawingObjectEnumerator viewObjects = activeView.GetAllObjects();
                    while (viewObjects.MoveNext())
                    {
                        if (viewObjects.Current is TSD.ReinforcementGroup rebar)
                        {
                            unassignedRebars.Add(rebar);
                        }
                    }

                    // Process each configuration (row) in the table sequentially
                    foreach (var rule in DimMappingRules)
                    {
                        if (string.IsNullOrEmpty(rule.DimProperty)) continue;

                        // KIỂM TRA DỪNG SỚM: Nếu đã gán hết thép thì không cần chạy các Rule còn lại nữa
                        if (unassignedRebars.Count == 0) break;

                        ArrayList rebarsToDim = new ArrayList();

                        // 2. DUYỆT NGƯỢC DANH SÁCH ĐỂ AN TOÀN KHI XÓA
                        for (int i = unassignedRebars.Count - 1; i >= 0; i--)
                        {
                            TSD.ReinforcementGroup rebar = unassignedRebars[i];

                            TSM.ModelObject modelObj = myModel.SelectModelObject(rebar.ModelIdentifier);

                            if (modelObj is TSM.Reinforcement modelRebar)
                            {
                                string rName = modelRebar.Name ?? "";
                                string prefix = "";
                                modelRebar.GetReportProperty("PREFIX", ref prefix);

                                bool matchName = rule.RebarName != null && rName.IndexOf(rule.RebarName, StringComparison.OrdinalIgnoreCase) >= 0;
                                bool matchPrefix = string.IsNullOrEmpty(rule.Prefix) || rule.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase);

                                if (matchName && matchPrefix)
                                {
                                    rebarsToDim.Add(rebar);

                                    // 3. XÓA THÉP NÀY KHỎI POOL (Rule sau sẽ không thấy thanh này nữa)
                                    unassignedRebars.RemoveAt(i);
                                }
                            }
                        }

                        if (rebarsToDim.Count > 0)
                        {
                            // 1. Highlight rebar objects
                            string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
                            string macroDir = GetMacroDirectory();
                            objectSelector.UnselectAllObjects();

                            System.Threading.Thread.Sleep(1000);

                            string templatePath = Path.Combine(appFolder, "Macro", "ChangeProperty.cs");
                            string macroContent = File.ReadAllText(templatePath);
                            macroContent = macroContent.Replace("<PROPERTY_NAME>", rule.DimProperty);
                            string tempMacroName = "Temp_Run_AutoDim.cs";
                            string tempRunPath = Path.Combine(macroDir, tempMacroName);
                            File.WriteAllText(tempRunPath, macroContent);
                            TSMO.Operation.RunMacro(@"..\drawings\" + tempMacroName);

                            if (rebarsToDim.Count == 1)
                            {
                                TSD.ReinforcementBase theOnlyRebar = rebarsToDim[0] as TSD.ReinforcementBase;
                                bool foundDummy = false;

                                // Dummy rebar search (Vẫn giữ nguyên lấy GetAllObjects vì Dummy có thể rơi vào các thép không thỏa mãn Rule nào)
                                TSD.DrawingObjectEnumerator allObjs = activeView.GetAllObjects();
                                while (allObjs.MoveNext())
                                {
                                    if (allObjs.Current is TSD.ReinforcementBase dummyRebar)
                                    {
                                        if (dummyRebar.ModelIdentifier.ID != theOnlyRebar.ModelIdentifier.ID)
                                        {
                                            TSM.ModelObject dummyModelObj = myModel.SelectModelObject(dummyRebar.ModelIdentifier);

                                            if (dummyModelObj is TSM.Reinforcement dummyRebarModel)
                                            {
                                                int barCount = 0;
                                                dummyRebarModel.GetReportProperty("NUMBER", ref barCount);

                                                if (barCount == 1)
                                                {
                                                    rebarsToDim.Add(dummyRebar);
                                                    foundDummy = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (rebarsToDim.Count == 1) { continue; }

                            objectSelector.SelectObjects(rebarsToDim, false);

                            string templatePath2 = Path.Combine(appFolder, "Macro", "DimForRebar.cs");
                            string macroContent2 = File.ReadAllText(templatePath2);
                            string tempMacroName2 = "Temp_AutoDim.cs";
                            string tempRunPath2 = Path.Combine(macroDir, tempMacroName2);
                            File.WriteAllText(tempRunPath2, macroContent2);
                            TSMO.Operation.RunMacro(@"..\drawings\" + tempMacroName2);
                        }
                    }
                }
                else
                {
                    MessageBox.Show("Please select at least one view in the drawing.");
                    return;
                }

                MessageBox.Show("Completed creating Dimensions for selected rebars based on profile '" + SelectedProfile + "'.", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        #endregion

        #region Rebar Tag

        /// <summary>
        /// Tự động tạo Tag (Mark) cho tất cả các cốt thép trong View đã chọn sử dụng Tekla native Mark API
        /// </summary>
        public void AutoTagSelectedView()
        {
            try
            {
                SaveCurrentTagProfile(false);

                TSD.DrawingHandler dh = new TSD.DrawingHandler();
                TSD.Drawing activeDrawing = dh.GetActiveDrawing();
                TSM.Model myModel = new TSM.Model();

                if (activeDrawing == null)
                {
                    MessageBox.Show("Please open a drawing first!");
                    return;
                }

                TSDUI.DrawingObjectSelector objectSelector = dh.GetDrawingObjectSelector();
                ArrayList selectedViews = new ArrayList();
                TSD.DrawingObjectEnumerator selectedObjects = objectSelector.GetSelected();

                while (selectedObjects.MoveNext())
                {
                    if (selectedObjects.Current is TSD.View view)
                    {
                        selectedViews.Add(view);
                    }
                }

                if (selectedViews.Count == 0)
                {
                    MessageBox.Show("Please select a view first!");
                    return;
                }

                // Xử lý từng rule (dòng) trong bảng Tag Mapping
                foreach (var rule in TagMappingRules)
                {
                    if (string.IsNullOrEmpty(rule.TagProperty)) continue;

                    foreach (TSD.View view in selectedViews)
                    {
                        TSD.DrawingObjectEnumerator viewObjects = view.GetAllObjects();
                        while (viewObjects.MoveNext())
                        {
                            if (viewObjects.Current is TSD.ReinforcementBase rebar)
                            {
                                TSM.ModelObject modelObj = myModel.SelectModelObject(rebar.ModelIdentifier);

                                if (modelObj is TSM.Reinforcement modelRebar)
                                {
                                    string rName = modelRebar.Name ?? "";
                                    string prefix = "";
                                    modelRebar.GetReportProperty("PREFIX", ref prefix);

                                    bool matchName = rule.RebarName != null && rName.Contains(rule.RebarName);
                                    bool matchPrefix = string.IsNullOrEmpty(rule.Prefix) || rule.Prefix.Equals(prefix, StringComparison.OrdinalIgnoreCase);

                                    if (matchName && matchPrefix)
                                    {
                                        TSD.Mark.MarkAttributes markAttr = new TSD.Mark.MarkAttributes(rebar, rule.TagProperty);
                                        TSD.Mark newMark = new TSD.Mark(rebar);


                                        if (newMark.Insert())
                                        {
                                            // In ra màn hình Output của Visual Studio để chắc chắn lệnh Insert đã pass
                                            System.Diagnostics.Debug.WriteLine($"Đã tạo tag thành công cho thép: {rName}");
                                        }

                                        if (newMark.Attributes.LoadAttributes(rule.TagProperty))
                                        {
                                            //
                                        }

                                        newMark.Modify();

                                    }
                                }
                            }
                        }
                    }
                }

                // BẮT BUỘC PHẢI CÓ DÒNG NÀY ĐỂ BẢN VẼ LƯU VÀ HIỂN THỊ
                activeDrawing.CommitChanges();

                MessageBox.Show($"Completed creating Tags for rebar(s) based on tag profile '{SelectedTagProfile}'.", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Lỗi: " + ex.Message);
            }
        }

        #endregion

        #region Helpers

        private string GetMacroDirectory()
        {
            string macroDir = string.Empty;
            TS.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroDir);
            if (string.IsNullOrEmpty(macroDir)) return string.Empty;
            if (macroDir.Contains(";")) macroDir = macroDir.Split(';')[0];

            string drawingMacroPath = Path.Combine(macroDir, "drawings");
            if (!Directory.Exists(drawingMacroPath)) Directory.CreateDirectory(drawingMacroPath);

            return drawingMacroPath;
        }

        #endregion
    }
}