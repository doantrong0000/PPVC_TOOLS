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
            this.DataContext = _viewModel;
            dgRebars.ItemsSource = _viewModel.Rebars;
        }

        private void BtnPickPart_Click(object sender, RoutedEventArgs e)
        {
            try 
            {
                var btn = sender as Button;
                if (btn != null) btn.IsEnabled = false;

                this.txtStatus.Text = "ACTIVE: Check Tekla selection...";
                _viewModel.Rebars.Clear();
                this.txtNoData.Visibility = Visibility.Visible;

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

        private void DgRebars_PreviewMouseWheel(object sender, System.Windows.Input.MouseWheelEventArgs e)
        {
            var scrollViewer = FindVisualChild<ScrollViewer>(dgRebars);
            if (scrollViewer != null)
            {
                if (e.Delta > 0)
                {
                    for (int i = 0; i < 3; i++) scrollViewer.LineUp();
                }
                else
                {
                    for (int i = 0; i < 3; i++) scrollViewer.LineDown();
                }
                e.Handled = true;
            }
        }

        private static T FindVisualChild<T>(DependencyObject obj) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(obj); i++)
            {
                DependencyObject child = System.Windows.Media.VisualTreeHelper.GetChild(obj, i);
                if (child != null && child is T)
                    return (T)child;
                else
                {
                    T childOfChild = FindVisualChild<T>(child);
                    if (childOfChild != null)
                        return childOfChild;
                }
            }
            return null;
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

        private void BtnExportJson_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportToJson();
        }

        // ===== Preview Actions (update grid only, no Tekla write) =====

        private void BtnAutoName_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.PreviewAutoName();
        }

        private void BtnAutoVH_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Rebars.Count == 0) { txtStatus.Text = "No rebars loaded. Pick part first."; return; }

            txtStatus.Text = "Preview: Assigning V/H prefixes...";
            string result = _viewModel.PreviewAutoVH();
            txtStatus.Text = result;
            dgRebars.Items.Refresh();
        }

        private void BtnAutoColor_Click(object sender, RoutedEventArgs e)
        {
            txtStatus.Text = "Preview: Assigning Class by Size...";
            string result = _viewModel.PreviewAutoColor();
            txtStatus.Text = result;
            dgRebars.Items.Refresh();
        }

        private void BtnSettings_Click(object sender, RoutedEventArgs e)
        {
            var window = new RebarSettingsWindow(_viewModel);
            window.ShowDialog();
        }

        private void BtnRunNumbering_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Rebars.Count == 0) { txtStatus.Text = "No rebars loaded. Pick part first."; return; }

            txtStatus.Text = "Preview: Numbering (skip existing)...";
            string result = _viewModel.PreviewNumbering(false);
            txtStatus.Text = result;
            dgRebars.Items.Refresh();
        }

        private void BtnReassignAll_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Rebars.Count == 0) { txtStatus.Text = "No rebars loaded. Pick part first."; return; }

            txtStatus.Text = "Preview: Reassigning all numbers...";
            string result = _viewModel.PreviewNumbering(true);
            txtStatus.Text = result;
            dgRebars.Items.Refresh();
        }

        // ===== Row-level actions =====

        private void BtnRevertRow_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is RebarInfoItem item)
            {
                if (item.IsIncluded)
                {
                    // First click: exclude from commit
                    item.IsIncluded = false;
                    txtStatus.Text = $"Excluded rebar {item.Id} from changes.";
                }
                else
                {
                    // Second click: revert to original values
                    item.RevertToOriginal();
                    txtStatus.Text = $"Reverted rebar {item.Id} to original values.";
                }
                dgRebars.Items.Refresh();
            }
        }

        // ===== Revert All + Commit to Tekla =====

        private void BtnRevertAll_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Rebars.Count == 0) { txtStatus.Text = "No rebars loaded."; return; }

            int count = 0;
            foreach (var item in _viewModel.Rebars)
            {
                if (item.IsChanged)
                {
                    item.RevertToOriginal();
                    count++;
                }
            }
            dgRebars.Items.Refresh();
            txtStatus.Text = count > 0 ? $"Reverted {count} rebars to original values." : "No pending changes to revert.";
        }

        private void BtnApply_Click(object sender, RoutedEventArgs e)
        {
            if (_viewModel.Rebars.Count == 0) { txtStatus.Text = "No rebars loaded."; return; }

            // Check if any changes exist
            bool hasChanges = false;
            foreach (var item in _viewModel.Rebars)
            {
                if (item.IsChanged && item.IsIncluded) { hasChanges = true; break; }
            }

            if (!hasChanges)
            {
                txtStatus.Text = "No pending changes to apply.";
                return;
            }

            var confirm = MessageBox.Show(
                "Apply all highlighted changes to Tekla model?",
                "Confirm Apply",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm == MessageBoxResult.Yes)
            {
                txtStatus.Text = "APPLYING changes to Tekla...";
                string result = _viewModel.CommitChangesToTekla();

                // Refresh from Tekla to show actual state
                string refreshResult = _viewModel.RefreshFromTekla();
                txtStatus.Text = result + " " + refreshResult;
                dgRebars.Items.Refresh();
            }
        }
    }
}
