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

        private void BtnSaveProfile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveCurrentProfile();
        }

        private void BtnSaveAsProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtSaveAs.Text))
            {
                _viewModel.SaveAsProfile(txtSaveAs.Text.Trim());
                txtSaveAs.Clear();
            }
            else
            {
                MessageBox.Show("Please enter a profile name to save as.");
            }
        }

        private void BtnAddRow_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.DimMappingRules.Add(new ViewModels.PageModels.DimMappingRule());
        }

        private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgMappingRules.SelectedItem is ViewModels.PageModels.DimMappingRule selectedRule)
            {
                _viewModel.DimMappingRules.Remove(selectedRule);
            }
            else
            {
                MessageBox.Show("Vui lòng chọn một dòng để xóa!");
            }
        }

        private void BtnAddProperty_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtNewProperty.Text))
            {
                _viewModel.AddNewProperty(txtNewProperty.Text.Trim());
                txtNewProperty.Clear();
            }
        }

        private void BtnAutoDimView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.AutoDimSelectedView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }
    }
}
