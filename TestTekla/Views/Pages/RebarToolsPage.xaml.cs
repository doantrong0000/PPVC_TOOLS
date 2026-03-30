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
            txtQuickRebarSpacing.Text = settings.Spacing;
            txtQuickRebarStart.Text = settings.StartOffset;
            txtQuickRebarEnd.Text = settings.EndOffset;
            txtQuickRebarOnPlane.Text = settings.OnPlaneOffset;
            txtQuickRebarName.Text = settings.RebarName;
            txtQuickRebarSize.Text = settings.RebarSize;
            txtQuickRebarGrade.Text = settings.RebarGrade;
            txtQuickRebarClass.Text = settings.RebarClass;
            chkMergeGroups.IsChecked = settings.MergeGroups;
        }

        private void SavePersistentSettings()
        {
            var settings = SettingsService.LoadSettings();
            settings.Spacing = txtQuickRebarSpacing.Text;
            settings.StartOffset = txtQuickRebarStart.Text;
            settings.EndOffset = txtQuickRebarEnd.Text;
            settings.OnPlaneOffset = txtQuickRebarOnPlane.Text;
            settings.RebarName = txtQuickRebarName.Text;
            settings.RebarSize = txtQuickRebarSize.Text;
            settings.RebarGrade = txtQuickRebarGrade.Text;
            settings.RebarClass = txtQuickRebarClass.Text;
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

        private void BtnQuickRebarMulti_Click(object sender, RoutedEventArgs e)
        {
            SavePersistentSettings();
            
            if (!double.TryParse(txtQuickRebarSpacing.Text, out double targetSpace) || targetSpace <= 0) return;
            if (!double.TryParse(txtQuickRebarStart.Text, out double startOffset)) startOffset = 0;
            if (!double.TryParse(txtQuickRebarEnd.Text, out double endOffset)) endOffset = 0;
            if (!double.TryParse(txtQuickRebarOnPlane.Text, out double onPlaneOffset)) onPlaneOffset = 0;
            if (!int.TryParse(txtQuickRebarClass.Text, out int rebarClass)) rebarClass = 2;

            _createVm.CreateRebarWithMultiPoints(
                targetSpace, 
                startOffset, 
                endOffset, 
                onPlaneOffset,
                txtQuickRebarName.Text,
                txtQuickRebarSize.Text,
                txtQuickRebarGrade.Text,
                rebarClass,
                chkMergeGroups.IsChecked ?? true
            );
        }

        private void BtnFindRebar_Click(object sender, RoutedEventArgs e)
        {
            _createVm.RunFindRebar();
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

        private void BtnShowRebarNumbering_Click(object sender, RoutedEventArgs e)
        {
            RebarSubContent.Content = new RebarNumberingPage();
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
