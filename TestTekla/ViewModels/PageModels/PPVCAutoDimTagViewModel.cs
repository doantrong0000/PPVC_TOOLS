using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Windows;

// Alias cho Tekla Drawing API
using TSD = Tekla.Structures.Drawing;
using TSDUI = Tekla.Structures.Drawing.UI;
using TSMO = Tekla.Structures.Model.Operations;
using TSS = Tekla.Structures;
using TSM = Tekla.Structures.Model;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;

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

    public class PPVCAutoDimTagViewModel : INotifyPropertyChanged
    {
        public ObservableCollection<DimMappingRule> DimMappingRules { get; set; }
        public ObservableCollection<string> AvailableDimProperties { get; set; }
        public ObservableCollection<string> AvailableProfiles { get; set; }

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

        private Dictionary<string, List<DimMappingRule>> _allProfiles;

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

            LoadDimProperties();
            LoadDimMappingRules();
        }

        private string GetJsonFilePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string folderPath = Path.Combine(appData, "TeklaTools", "Json");
            if (!Directory.Exists(folderPath))
            {
                Directory.CreateDirectory(folderPath);
            }
            return Path.Combine(folderPath, "DimProperties.json");
        }

        private void LoadDimProperties()
        {
            string jsonFilePath = GetJsonFilePath();
            AvailableDimProperties = new ObservableCollection<string>();

            if (File.Exists(jsonFilePath))
            {
                try
                {
                    string jsonContent = File.ReadAllText(jsonFilePath);
                    // Parse thủ công bằng Regex để đọc mảng chuỗi JSON mà không lo thiếu thư viện
                    var matches = System.Text.RegularExpressions.Regex.Matches(jsonContent, "\"([^\"]+)\"");
                    foreach (System.Text.RegularExpressions.Match m in matches)
                    {
                        AvailableDimProperties.Add(m.Groups[1].Value);
                    }
                }
                catch { }
            }

            // Nếu chưa có file JSON hoặc đọc bị rỗng, tạo mặc định
            if (AvailableDimProperties.Count == 0)
            {
                AvailableDimProperties.Add("A1");
                AvailableDimProperties.Add("A2");
                AvailableDimProperties.Add("A3");
                AvailableDimProperties.Add("WH_ArialNarr_2mm_Trans_Links Dim_no pic_Bar Middle Group_LINKS TEXT");

                string jsonFilePathCreate = GetJsonFilePath();
                SaveDimProperties(jsonFilePathCreate);
            }
        }

        private void SaveDimProperties(string jsonFilePath)
        {
            try
            {
                // Sinh chuỗi JSON chuẩn
                System.Text.StringBuilder sb = new System.Text.StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < AvailableDimProperties.Count; i++)
                {
                    sb.Append("  \"");
                    sb.Append(AvailableDimProperties[i]);
                    sb.Append("\"");
                    if (i < AvailableDimProperties.Count - 1) sb.AppendLine(",");
                    else sb.AppendLine();
                }
                sb.AppendLine("]");
                File.WriteAllText(jsonFilePath, sb.ToString());
            }
            catch { }
        }

        public void AddNewProperty(string newProperty)
        {
            if (!AvailableDimProperties.Contains(newProperty))
            {
                AvailableDimProperties.Add(newProperty);
                string jsonFilePath = GetJsonFilePath();
                SaveDimProperties(jsonFilePath);
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

        #region Rebar Dimension

        // 1. THÊM TỪ KHÓA 'async' VÀO ĐÂY
        /// <summary>
        /// Tự động tạo Dimension cho tất cả cốt thép trong View đang được chọn
        /// </summary>
        public async void AutoDimSelectedView()
        {
            try
            {
                SaveCurrentProfile(false);

                TSD.DrawingHandler dh = new TSD.DrawingHandler();
                TSD.Drawing activeDrawing = dh.GetActiveDrawing();
                TSM.Model myModel = new TSM.Model();

                if (activeDrawing == null)
                {
                    MessageBox.Show("Vui lòng mở một bản vẽ (Drawing) trước!");
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

                if (selectedViews.Count == 0) return;

                // Xử lý LẦN LƯỢT từng cấu hình (dòng) trong bảng
                foreach (var rule in DimMappingRules)
                {
                    if (string.IsNullOrEmpty(rule.DimProperty)) continue;

                    ArrayList rebarsToDim = new ArrayList();

                    // Quét thép trong các View được chọn
                    foreach (TSD.View view in selectedViews)
                    {
                        TSD.DrawingObjectEnumerator viewObjects = view.GetAllObjects();
                        while (viewObjects.MoveNext())
                        {
                            if (viewObjects.Current is TSD.ReinforcementGroup rebar)
                            {
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
                                    }
                                }
                            }
                        }
                    }

                    // Chạy Macro chỉ cho nhóm thép khớp với dòng thiết lập này
                    if (rebarsToDim.Count > 0)
                    {
                        // 1. Highlight đối tượng thép
                        objectSelector.SelectObjects(rebarsToDim, false);

                        // 2. Lấy đường dẫn file Template
                        string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
                        string macroDir = GetMacroDirectory();


                        string templatePath2 = Path.Combine(appFolder, "Macro", "DimForRebar.cs");
                        string macroContent2 = File.ReadAllText(templatePath2);
                        string tempMacroName2 = "Temp_AutoDim.cs";
                        string tempRunPath2 = Path.Combine(macroDir, tempMacroName2);
                        File.WriteAllText(tempRunPath2, macroContent2);
                        TSMO.Operation.RunMacro(@"..\drawings\" + tempMacroName2);


                        string templatePath = Path.Combine(appFolder, "Macro", "ChangeProperty.cs");

                        // 3. Đọc nội dung và "Bơm" tên thuộc tính tương ứng của Rule (VD: A1, A2) vào
                        string macroContent = File.ReadAllText(templatePath);
                        macroContent = macroContent.Replace("<TEN_THUOC_TINH>", rule.DimProperty);

                        // 4. Lưu ra thư mục Macro của Tekla để Tekla có thể chạy được

                        string tempMacroName = "Temp_Run_AutoDim.cs";
                        string tempRunPath = Path.Combine(macroDir, tempMacroName);
                        File.WriteAllText(tempRunPath, macroContent);
                        TSMO.Operation.RunMacro(@"..\drawings\" + tempMacroName);





                    }
                }
                MessageBox.Show("Đã hoàn thành tạo Dimension cho các thép đã chọn theo profile '" + SelectedProfile + "'.", "Hoàn thành", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                // MessageBox.Show("Lỗi thực thi: " + ex.Message);
            }
        }

        /// <summary>
        /// SỬ DỤNG LẠI MACRO MỞ UI ĐÃ ĐƯỢC CHỨNG MINH LÀ CHẠY ĐÚNG THUỘC TÍNH
        /// </summary>
        private void CreateFastExecuteDimMacro(string macroName, string propertyName)
        {
            string macroPath = GetMacroDirectory();
            if (string.IsNullOrEmpty(macroPath)) return;

            string fullPath = Path.Combine(macroPath, macroName);

            string macros =
               @"#pragma warning disable 1633 // Unrecognized #pragma directive" + "\r\n" +
               @"#pragma reference ""Tekla.Macros.Wpf.Runtime""" + "\r\n" +
               @"#pragma reference ""Tekla.Macros.Akit""" + "\r\n" +
               @"#pragma reference ""Tekla.Macros.Runtime""" + "\r\n" +
               @"#pragma warning restore 1633 // Unrecognized #pragma directive" + "\r\n" +

               @"namespace UserMacros {" + "\r\n" +
                   @"public sealed class Macro {" + "\r\n" +

                       @"[Tekla.Macros.Runtime.MacroEntryPointAttribute()]" + "\r\n" +
                       @"public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {" + "\r\n" +
                           @"Tekla.Macros.Akit.IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();" + "\r\n" +

                           @"Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf = runtime.Get<Tekla.Macros.Wpf.Runtime.IWpfMacroHost>();" + "\r\n" +
                              @"System.Threading.Thread.Sleep(1000);" + "\r\n" +
                            @"akit.ValueChange(""rebar_dim_dial"", ""gr_dim_get_menu"", " + "\"" + propertyName + "\"" + ");" + "\r\n" +

                           @"akit.PushButton(""gr_dim_get"", ""rebar_dim_dial"");" + "\r\n" +

                           @"akit.PushButton(""dim_apply"", ""rebar_dim_dial"");" + "\r\n" +

                           @"akit.PushButton(""dim_ok"", ""rebar_dim_dial"");" + "\r\n" +



                           @"wpf.InvokeCommand(""CommandRepository"", ""Dimensions.AddRebarDimensionMark"");" + "\r\n" +

                           @"akit.CommandEnd();" + "\r\n" +
                       @"}" + "\r\n" +
                   @"}" + "\r\n" +
               @"}";
            File.WriteAllText(fullPath, macros);
        }

        private string GetMacroDirectory()
        {
            string macroDir = string.Empty;
            TSS.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroDir);
            if (string.IsNullOrEmpty(macroDir)) return string.Empty;
            if (macroDir.Contains(";")) macroDir = macroDir.Split(';')[0];

            string drawingMacroPath = Path.Combine(macroDir, "drawings");
            if (!Directory.Exists(drawingMacroPath)) Directory.CreateDirectory(drawingMacroPath);

            return drawingMacroPath;
        }

        #endregion
    }
}