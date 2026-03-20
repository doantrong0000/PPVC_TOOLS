using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarInspectorPage : UserControl
    {
        private RebarInspectorViewModel _viewModel;

        public RebarInspectorPage()
        {
            InitializeComponent();
            _viewModel = new RebarInspectorViewModel();
            dgRebars.ItemsSource = _viewModel.Rebars;
        }

        private void BtnPickPart_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                // Disable button and show status to prevent multi-click (Rule #2)
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = false;

                this.txtStatus.Text = "ACTIVE: Check Tekla selection...";
                _viewModel.Rebars.Clear();
                this.txtNoData.Visibility = Visibility.Visible;

                // Rule #1: Pre-selection check is inside GetRebarData
                string statusText;
                var data = _viewModel.GetRebarData(out statusText);
                
                foreach (var item in data) 
                {
                    _viewModel.Rebars.Add(item);
                }

                this.txtSelectedPart.Text = _viewModel.SelectedObjectName;
                this.txtStatus.Text = statusText;
                this.txtNoData.Visibility = _viewModel.Rebars.Count > 0 ? Visibility.Collapsed : Visibility.Visible;
                
                if (btn != null) btn.IsEnabled = true;
            }
            catch (Exception ex)
            {
                this.txtSelectedPart.Text = "None";
                this.txtStatus.Text = "Error: " + ex.Message;
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = true;
            }
        }

        private void DgRebars_Click(object sender, SelectionChangedEventArgs e)
        {
            if (dgRebars.SelectedItem is RebarInfoItem selectedRebar)
            {
                _viewModel.SelectRebarInTekla(selectedRebar.Id);
                this.txtStatus.Text = "Selected rebar ID: " + selectedRebar.Id;
            }
        }

        private void ToggleCol_Click(object sender, RoutedEventArgs e)
        {
            if (sender is CheckBox chk && chk.Tag != null)
            {
                string colName = chk.Tag.ToString();
                var column = this.dgRebars.FindName(colName) as DataGridColumn;
                
                if (column != null)
                {
                    column.Visibility = chk.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
                }
            }
        }
    }
}
