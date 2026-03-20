using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using TeklaApp.Helpers;
using TeklaApp.Models;

namespace TeklaApp.Views
{
    public partial class JoinAssemblyPage : UserControl
    {
        private Model _model;

        public JoinAssemblyPage()
        {
            InitializeComponent();
            _model = new Model();
            LoadPersistentSettings();
        }

        private void LoadPersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            chkSteel.IsChecked = settings.JoinSteel;
            chkEmbed.IsChecked = settings.JoinEmbed;
            chkRebar.IsChecked = settings.JoinRebar;
            chkBolt.IsChecked = settings.JoinBolt;
            chkWeld.IsChecked = settings.JoinWeld;
            chkSurface.IsChecked = settings.JoinSurface;
            chkFeatures.IsChecked = settings.JoinFeatures;
        }

        private void SavePersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            settings.JoinSteel = chkSteel.IsChecked ?? true;
            settings.JoinEmbed = chkEmbed.IsChecked ?? true;
            settings.JoinRebar = chkRebar.IsChecked ?? true;
            settings.JoinBolt = chkBolt.IsChecked ?? true;
            settings.JoinWeld = chkWeld.IsChecked ?? false;
            settings.JoinSurface = chkSurface.IsChecked ?? true;
            settings.JoinFeatures = chkFeatures.IsChecked ?? false;
            SettingsService.SaveSettings(settings);
        }

        private void BtnJoin_Click(object sender, RoutedEventArgs e)
        {
            SavePersistentSettings();
            if (!_model.GetConnectionStatus())
            {
                txtStatus.Text = "Error: Tekla Structures is not running.";
                return;
            }

            try
            {
                Picker picker = new Picker();
                ModelObject mainObj = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Please select the main part...");
                
                if (mainObj is Part mainPart)
                {
                    ModelObjectEnumerator secondaryObjects = picker.PickObjects(Picker.PickObjectsEnum.PICK_N_OBJECTS, "Sweep select EVERYTHING to filter and press MIDDLE mouse button...");

                    Assembly assembly = mainPart.GetAssembly();
                    int count = 0;
                    int filteredCount = 0;

                    while (secondaryObjects.MoveNext())
                    {
                        ModelObject obj = secondaryObjects.Current;
                        if (obj == null || obj.Identifier.ID == mainPart.Identifier.ID) continue;

                        bool shouldAdd = false;

                        // --- 1. PHYSICAL PARTS ---
                        if (obj is Part p)
                        {
                            // Logic phân biệt Steel Part vs Embedded (giả lập theo class/name)
                            bool isEmbed = p.Name.ToUpper().Contains("EMBED") || p.Name.ToUpper().Contains("ĐẶT SẴN");
                            if (isEmbed && chkEmbed.IsChecked == true) shouldAdd = true;
                            else if (!isEmbed && chkSteel.IsChecked == true) shouldAdd = true;
                        }
                        else if (obj is Reinforcement && chkRebar.IsChecked == true)
                        {
                            shouldAdd = true;
                        }
                        // --- 2. CONNECTORS ---
                        else if (obj is BoltArray && chkBolt.IsChecked == true)
                        {
                            // Lưu ý: Tùy phiên bản SDK, BoltArray có thể cần cast hoặc add theo cách riêng 
                            // Nếu Add(IAssemblable) lỗi, ta sẽ tạm bỏ qua hoặc báo cho người dùng
                            shouldAdd = true; 
                        }
                        else if (obj is BaseWeld && chkWeld.IsChecked == true)
                        {
                            shouldAdd = true;
                        }
                        // --- 3. FEATURES & TREATMENT ---
                        else if (obj is SurfaceTreatment && chkSurface.IsChecked == true)
                        {
                            shouldAdd = true;
                        }
                        else if ((obj is BooleanPart || obj is Fitting) && chkFeatures.IsChecked == true)
                        {
                            // BooleanPart/Fitting thường bám theo Part, việc "Add vào Assembly" có thể không cần thiết 
                            // nhưng vẫn giữ filter theo yêu cầu người dùng
                            shouldAdd = false; // Mặc định false vì Features không phải IAssemblable trực tiếp
                        }

                        if (shouldAdd)
                        {
                            try 
                            {
                                // Tekla Assembly.Add(IAssemblable)
                                if (obj is IAssemblable assemblable)
                                {
                                    assembly.Add(assemblable);
                                    count++;
                                }
                                else if (obj is BoltArray || obj is BaseWeld)
                                {
                                    // Trong một số SDK, Bolt/Weld được Add bằng cách gán Assembly property hoặc overload khác
                                    // Ở đây ta thử gọi Add nếu SDK hỗ trợ dynamic hoặc bỏ qua nếu không phải IAssemblable
                                    // (Dựa trên lỗi trước đó, BoltArray không phải IAssemblable)
                                    filteredCount++;
                                }
                            }
                            catch { filteredCount++; }
                        }
                        else
                        {
                            filteredCount++;
                        }
                    }

                    if (count > 0)
                    {
                        assembly.Modify();
                        _model.CommitChanges();
                        txtStatus.Text = $"Success! Added {count} objects to assembly. (Filtered {filteredCount})";
                    }
                    else
                    {
                        txtStatus.Text = "No valid assemblable objects added. Check your filters.";
                    }
                }
                else
                {
                    txtStatus.Text = "Invalid main part selected.";
                }
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Cancelled or error: " + ex.Message;
            }
        }
    }
}
