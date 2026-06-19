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
using static System.Windows.Forms.VisualStyles.VisualStyleElement;
// Alias cho Tekla Drawing API
using TSD = Tekla.Structures.Drawing;
using TSDUI = Tekla.Structures.Drawing.UI;
using TSM = Tekla.Structures.Model;
using TSMO = Tekla.Structures.Model.Operations;
using TS = Tekla.Structures;

namespace TeklaApp.ViewModels.PageModels
{
    public class DimMappingRule : INotifyPropertyChanged
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

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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

        private string _statusMessage = "Ready";
        public string StatusMessage
        {
            get => _statusMessage;
            set { _statusMessage = value; OnPropertyChanged(); }
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
                StatusMessage = "Applying profile...";
                SaveCurrentProfile(false);

                TSD.DrawingHandler dh = new TSD.DrawingHandler();
                TSD.Drawing activeDrawing = dh.GetActiveDrawing();
                TSM.Model myModel = new TSM.Model();

                if (activeDrawing == null)
                {
                    StatusMessage = "Error: No active drawing.";
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
                    StatusMessage = "Error: No view selected.";
                    return;
                }

                // Process each configuration (row) in the table sequentially
                foreach (var rule in DimMappingRules)
                {
                    StatusMessage = $"Processing rule: {rule.RebarName} ({rule.Prefix})...";
                    if (string.IsNullOrEmpty(rule.DimProperty)) continue;


                    // Scan rebars in the selected Views
                    foreach (TSD.View view in selectedViews)
                    {
                        ArrayList rebarsToDim = new ArrayList();
                        TSD.DrawingObjectEnumerator viewObjects = view.GetAllObjects();
                        while (viewObjects.MoveNext())
                        {
                            if (viewObjects.Current is TSD.ReinforcementGroup rebar)
                            {
                                // 1. Kiểm tra xem Rebar này đã được gán Mark trong bản vẽ chưa
                                bool hasMark = false;
                                var associatedObjects = rebar.GetRelatedObjects();

                                while (associatedObjects.MoveNext())
                                {
                                    // Kiểm tra cả Mark thông thường và RebarMark chuyên dụng của cốt thép
                                    if (associatedObjects.Current is TSD.DimensionBase mark)
                                    {

                                        hasMark = true;
                                        break; // Đã tìm thấy thì thoát vòng lặp nhỏ này ngay
                                    }
                                }

                                // Nếu ĐÃ CÓ MARK: Cảnh báo người dùng và BỎ QUA không cho vào danh sách đi Dim
                                if (hasMark)
                                {
                                    continue; // Nhảy ngay sang thanh rebar tiếp theo trong view, không chạy code phía dưới nữa
                                }

                                // 2. Nếu CHƯA CÓ MARK: Tiến hành kết nối xuống mô hình để lọc theo Rule
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
                                        rebarsToDim.Add(rebar); // Thêm đối tượng bản vẽ hợp lệ vào danh sách chờ Dim
                                    }
                                }
                            }
                        }
                        if (rebarsToDim.Count > 0)
                        {
                            // 1. Highlight rebar objects
                            // 2. Get Template file path
                            string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
                            string macroDir = GetMacroDirectory();

                            System.Threading.Thread.Sleep(500);

                            objectSelector.UnselectAllObjects();

                            string templatePath = Path.Combine(appFolder, "Macro", "ChangeProperty.cs");
                            string macroContent = File.ReadAllText(templatePath);
                            macroContent = macroContent.Replace("<PROPERTY_NAME>", rule.DimProperty);
                            string tempMacroName = "Temp_Run_AutoDim.cs";
                            string tempRunPath = Path.Combine(macroDir, tempMacroName);
                            File.WriteAllText(tempRunPath, macroContent);
                            TSMO.Operation.RunMacro(@"..\drawings\" + tempMacroName);

                            if (rebarsToDim.Count == 1)
                            {
                                // 1. Explicitly cast to TSD.ReinforcementBase instead of TSD.DrawingObject
                                TSD.ReinforcementBase theOnlyRebar = rebarsToDim[0] as TSD.ReinforcementBase;
                                bool foundDummy = false;

                                foreach (TSD.View v in selectedViews)
                                {
                                    TSD.DrawingObjectEnumerator allObjs = v.GetAllObjects();
                                    while (allObjs.MoveNext())
                                    {
                                        if (allObjs.Current is TSD.ReinforcementBase dummyRebar)
                                        {
                                            // 2. Ensure ID is different from the rebar that needs Dim
                                            if (dummyRebar.ModelIdentifier.ID != theOnlyRebar.ModelIdentifier.ID)
                                            {
                                                // 3. Trace back to Model to check Quantity
                                                TSM.ModelObject dummyModelObj = myModel.SelectModelObject(dummyRebar.ModelIdentifier);

                                                if (dummyModelObj is TSM.Reinforcement dummyRebarModel)
                                                {
                                                    int barCount = 0;
                                                    // Get actual number of rebars in the model
                                                    dummyRebarModel.GetReportProperty("NUMBER", ref barCount);

                                                    // 4. PREREQUISITE: Only take bars with quantity EXACTLY EQUAL to 1
                                                    if (barCount == 1)
                                                    {
                                                        rebarsToDim.Add(dummyRebar); // Add to make Count = 2
                                                        foundDummy = true;
                                                        break; // Found one matching bar, exit while loop
                                                    }
                                                }
                                            }
                                        }
                                    }
                                    if (foundDummy) break; // Exit foreach loop (view)
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

                    // Run Macro only for the group of rebars matching this setup line

                }
                StatusMessage = "Completed.";
                MessageBox.Show("Completed creating Dimensions for selected rebars based on profile '" + SelectedProfile + "'.", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
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
                StatusMessage = "Applying tag profile...";
                SaveCurrentTagProfile(false);

                TSD.DrawingHandler dh = new TSD.DrawingHandler();
                TSD.Drawing activeDrawing = dh.GetActiveDrawing();
                TSM.Model myModel = new TSM.Model();

                if (activeDrawing == null)
                {
                    StatusMessage = "Error: No active drawing.";
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
                    StatusMessage = "Error: No view selected.";
                    MessageBox.Show("Please select a view first!");
                    return;
                }

                int totalTagged = 0;

                // Xử lý từng rule (dòng) trong bảng Tag Mapping
                foreach (var rule in TagMappingRules)
                {
                    StatusMessage = $"Processing tag rule: {rule.RebarName} ({rule.Prefix})...";
                    if (string.IsNullOrEmpty(rule.TagProperty)) continue;

                    foreach (TSD.View view in selectedViews)
                    {
                        // Tính tâm và bán kính tỏa tag từ RestrictionBox (tọa độ view)
                        var viewExtent = view.RestrictionBox;
                        double viewCenterX = (viewExtent.MinPoint.X + viewExtent.MaxPoint.X) / 2.0;
                        double viewCenterY = (viewExtent.MinPoint.Y + viewExtent.MaxPoint.Y) / 2.0;
                        double viewHalfW = (viewExtent.MaxPoint.X - viewExtent.MinPoint.X) / 2.0;
                        double viewHalfH = (viewExtent.MaxPoint.Y - viewExtent.MinPoint.Y) / 2.0;
                        double tagRadius = Math.Min(viewHalfW, viewHalfH) * 0.7; // 70% bán kính view

                        int tagCountInView = 0; // Đếm tag trong view để chia đều góc

                        TSD.DrawingObjectEnumerator viewObjects = view.GetAllObjects();
                        while (viewObjects.MoveNext())
                        {
                            if (viewObjects.Current is TSD.ReinforcementBase rebar)
                            {
                                // 1. Kiểm tra xem Rebar này đã có Tag (Mark) trong bản vẽ chưa
                                bool hasTag = false;
                                var relatedObjects = rebar.GetRelatedObjects();

                                while (relatedObjects.MoveNext())
                                {
                                    if (relatedObjects.Current is TSD.Mark)
                                    {
                                        hasTag = true;
                                        break;
                                    }
                                }

                                // Nếu đã có Tag thì bỏ qua
                                if (hasTag) continue;

                                // 2. Kết nối xuống Model để kiểm tra theo Rule
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
                                        // 3. Tạo Tag sử dụng Tekla native Mark API
                                        try
                                        {
                                            // Tính góc đặt tag dựa trên index để tỏa đều quanh view
                                            // Mỗi vòng (ring) chứa 8 tag, các vòng tiếp nhau xoay lệch nửa bước
                                            int tagsPerRing = 8;
                                            int ring = tagCountInView / tagsPerRing;
                                            double angleStep = 2.0 * Math.PI / tagsPerRing;
                                            double angle = tagCountInView * angleStep + ring * (angleStep / 2.0);
                                            double radius = tagRadius + ring * 30.0;

                                            // Điểm chèn tag: tỏa ra từ tâm view theo góc, leader line sẽ tự kéo về rebar
                                            double insertX = viewCenterX + radius * Math.Cos(angle);
                                            double insertY = viewCenterY + radius * Math.Sin(angle);

                                            TSD.Mark newMark = new TSD.Mark(rebar);

                                            // Load thuộc tính tag từ file .RM attribute
                                            newMark.Attributes.LoadAttributes(rule.TagProperty);

                                            // Thiết lập vị trí đặt tag text (leader line tự động kéo từ đây về rebar)
                                            newMark.InsertionPoint = new Tekla.Structures.Geometry3d.Point(insertX, insertY, 0);

                                            // Chèn Mark vào bản vẽ
                                            newMark.Insert();

                                            tagCountInView++;
                                            totalTagged++;
                                        }
                                        catch (Exception markEx)
                                        {
                                            // Ghi log lỗi nhưng tiếp tục xử lý các rebar khác
                                            System.Diagnostics.Debug.WriteLine($"Failed to create tag for rebar '{rName}': {markEx.Message}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                activeDrawing.CommitChanges();
                StatusMessage = $"Completed. Tagged {totalTagged} rebar(s).";
                MessageBox.Show($"Completed creating Tags for {totalTagged} rebar(s) based on tag profile '{SelectedTagProfile}'.", "Completed", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                StatusMessage = "Error: " + ex.Message;
                MessageBox.Show("Error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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