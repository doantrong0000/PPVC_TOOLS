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

        #region Dim Profile Handlers

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

        private void BtnDeleteProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedProfile == null) return;
            
            if (_viewModel.SelectedProfile.Equals("standard", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete the 'standard' profile.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete the profile '{_viewModel.SelectedProfile}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteCurrentProfile();
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
                MessageBox.Show("Please select a row to delete!");
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

        #endregion

        #region Tag Profile Handlers

        private void BtnSaveTagProfile_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.SaveCurrentTagProfile();
        }

        private void BtnSaveAsTagProfile_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(txtTagSaveAs.Text))
            {
                _viewModel.SaveAsTagProfile(txtTagSaveAs.Text.Trim());
                txtTagSaveAs.Clear();
            }
            else
            {
                MessageBox.Show("Please enter a tag profile name to save as.");
            }
        }

        private void BtnDeleteTagProfile_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.SelectedTagProfile == null) return;

            if (_viewModel.SelectedTagProfile.Equals("standard", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Cannot delete the 'standard' tag profile.", "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show($"Are you sure you want to delete the tag profile '{_viewModel.SelectedTagProfile}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result == MessageBoxResult.Yes)
            {
                _viewModel.DeleteCurrentTagProfile();
            }
        }

        private void BtnAddTagRow_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.TagMappingRules.Add(new ViewModels.PageModels.TagMappingRule());
        }

        private void BtnDeleteTagRow_Click(object sender, RoutedEventArgs e)
        {
            if (dgTagMappingRules.SelectedItem is ViewModels.PageModels.TagMappingRule selectedRule)
            {
                _viewModel.TagMappingRules.Remove(selectedRule);
            }
            else
            {
                MessageBox.Show("Please select a tag row to delete!");
            }
        }

        private void BtnAutoTagView_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _viewModel.AutoTagSelectedView();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
        }

        #endregion
    }
}
