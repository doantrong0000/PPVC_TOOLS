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
        }

        private void SavePersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            SettingsService.SaveSettings(settings);
        }

        private void BtnReverseRebar_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ReverseRebarDistribution();
        }



        private void BtnSelectRebarsOfPart_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SelectRebarsOfPart();
        }

        private void BtnAlignToPlane_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AlignSelectedRebarsToPlane();
        }

        private void BtnSplitRebar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _createVm.SplitRebar();
            }
            catch
            {
                //
            }

        }

        private void BtnQuickRebarMulti_Click(object sender, RoutedEventArgs e)
        {
            SavePersistentSettings();
            _createVm.StatusMessage = "Creating rebars (cloning from source)...";
            _createVm.CloneRebarWithMultiPoints(double.Parse(txtCover.Text), double.Parse(txtSpacingTarget.Text));
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

        private void btnAddPoint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _createVm.AddPointInRebar();
            }
            catch
            {
                //
            }
        }

        private void BtnReversePointRebar_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _createVm.ReversePointRebar();
            }
            catch
            {
                //
            }
        }

        private void btnDeletePoint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _createVm.DeletePointRebar();
            }
            catch
            {
                //
            }
        }
    }
}
