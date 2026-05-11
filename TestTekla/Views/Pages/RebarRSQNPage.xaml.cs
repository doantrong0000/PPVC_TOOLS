using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarRSQNPage : UserControl
    {
        public RebarRSQNViewModel ViewModel { get; }

        public RebarRSQNPage()
        {
            InitializeComponent();
            ViewModel = new RebarRSQNViewModel();
            this.DataContext = ViewModel;
        }

        private void BtnGetRebar_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GetRebarsFromModel();
        }

        private void BtnSelectInModel_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            ViewModel.SelectInModel(selectedItems);
        }

        private void BtnChange_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ExecuteChange();
        }

        private void BtnUpdate_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.UpdateInModel();
        }

        private void BtnFindCheckAgain_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CheckAgainAndSort();
        }

        private void BtnFindOverlap_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.CheckOverlapAndSort();
        }

        private void ChkShowSelected_Checked(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ChkShowSelected_Unchecked(object sender, RoutedEventArgs e)
        {
            ApplyFilter();
        }

        private void ApplyFilter()
        {
            if (chkShowSelected.IsChecked == true)
            {
                TriggerShowOnlySelectedInTekla();
            }
            else
            {
                // If unchecked, maybe redraw the whole view to show all
                try {
                    Tekla.Structures.Model.Operations.Operation.RunMacro("View_RedrawAll");
                } catch {}
            }
        }

        private void DgvRebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvRebar.SelectedItems == null) return;
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            var seqs = selectedItems.Where(x => x.Seq > 0).Select(x => x.Seq).Distinct().OrderBy(s => s).Select(s => s.ToString());
            txtSelectedInfo.Text = string.Join(" ", seqs);

            // If active, run Tekla ShowOnlySelected so only the newly selected items are shown in the model
            if (chkShowSelected.IsChecked == true)
            {
                TriggerShowOnlySelectedInTekla();
            }
        }

        private void TriggerShowOnlySelectedInTekla()
        {
            if (dgvRebar.SelectedItems == null) return;
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            ViewModel.SelectInModel(selectedItems);

            try
            {
                Tekla.Structures.Model.Operations.Operation.RunMacro("View_ShowOnlySelected");
            }
            catch { }
        }

        private void DgvRebar_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            ViewModel.SelectInModel(selectedItems);
        }

        private void NumberValidationTextBox(object sender, System.Windows.Input.TextCompositionEventArgs e)
        {
            System.Text.RegularExpressions.Regex regex = new System.Text.RegularExpressions.Regex("[^0-9.]+");
            e.Handled = regex.IsMatch(e.Text);
        }
    }
}
