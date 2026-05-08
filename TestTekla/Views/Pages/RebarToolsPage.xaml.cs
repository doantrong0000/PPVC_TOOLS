using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;
using TeklaApp.ViewModels.PageModels;
using TeklaApp.Helpers;
using TeklaApp.Models;

namespace TeklaApp.Views.Pages
{
    public partial class RebarToolsPage : UserControl
    {
        private MainViewModel _viewModel;
        private CreateRebarViewModel _createVm;

        public RebarToolsPage()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _createVm = new CreateRebarViewModel();
            this.DataContext = _createVm;
            LoadPersistentSettings();
        }

        private void LoadPersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            chkMergeGroups.IsChecked = settings.MergeGroups;
        }

        private void SavePersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            settings.MergeGroups = chkMergeGroups.IsChecked ?? true;
            SettingsService.SaveSettings(settings);
        }

        private void BtnReverseRebar_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ReverseRebarDistribution();
        }

        private void BtnRepickRebarRange_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RepickRebarRange();
        }

        private void BtnSelectRebarsOfPart_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectRebarsOfPart();
        }
        
        private void BtnSplitRebar_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SplitRebarDistribution();
        }

        private void BtnAlignToPlane_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AlignSelectedRebarsToPlane();
        }

        private void BtnQuickRebarMulti_Click(object sender, RoutedEventArgs e)
        {
            SavePersistentSettings();
            _createVm.StatusMessage = "Creating rebars (cloning from source)...";
            bool merge = chkMergeGroups.IsChecked == true;
            _createVm.CloneRebarWithMultiPoints(merge);
        }

        private void BtnFindRebar_Click(object sender, RoutedEventArgs e)
        {
            _createVm.RunFindRebar();
        }

        private void BtnPickAssembly_Click(object sender, RoutedEventArgs e)
        {
            _createVm.PickAssembly();
        }

        private void TxtFindSeq_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _createVm.RunFindRebar();
            }
        }

        private void BtnShowRebarInspector_Click(object sender, RoutedEventArgs e)
        {
            RebarSubContent.Content = new RebarInspectorPage();
            txtEmptyState.Visibility = Visibility.Collapsed;
            btnCloseSubView.Visibility = Visibility.Visible;
        }



        private void BtnCloseSubView_Click(object sender, RoutedEventArgs e)
        {
            RebarSubContent.Content = null;
            txtEmptyState.Visibility = Visibility.Visible;
            btnCloseSubView.Visibility = Visibility.Collapsed;
        }
    }
}
