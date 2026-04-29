using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels.PageModels;

namespace TeklaApp.Views.Pages
{
    public partial class PPVCAutoDimTagPage : UserControl
    {
        private PPVCAutoDimTagViewModel _viewModel;

        public PPVCAutoDimTagPage()
        {
            InitializeComponent();
            _viewModel = new PPVCAutoDimTagViewModel();
            this.DataContext = _viewModel;
        }

        private void BtnCreateCastUnitDrawing_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Creating drawing...";
                string settingName = txtSettings.Text;
                if (string.IsNullOrWhiteSpace(settingName)) settingName = "+ZRB_PPVC_CASTUNIT_DWG";
                _viewModel.CreateCastUnitDrawing(settingName, out string status);
                txtStatus.Text = status;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
            }
        }

        private void BtnCreateSections_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Creating sections...";
                _viewModel.CreateBasicSections(out string status);
                txtStatus.Text = status;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
            }
        }
    }
}
