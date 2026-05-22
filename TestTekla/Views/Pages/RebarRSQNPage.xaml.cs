using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
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
            if (chkShowSelected.IsChecked == true)
            {
                TriggerShowOnlySelectedInTekla();
            }
            else
            {
                ViewModel.SelectInModel(selectedItems);
            }
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
            // Condition for SELECT button, no immediate action
        }

        private void ChkShowSelected_Unchecked(object sender, RoutedEventArgs e)
        {
            // Redraw to show all when unchecking
            try
            {
                Tekla.Structures.Model.Operations.Operation.RunMacro("View_RedrawAll");
            }
            catch { }
        }


        private void DgvRebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvRebar.SelectedItems == null) return;
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            var seqs = selectedItems
                .SelectMany(x => (x.Seq == 0.001 || x.Note == "Overlap") ? x.ExistingSequences : new List<double> { x.Seq })
                .Where(s => s > 0 && s != 0.001)
                .Distinct()
                .OrderBy(s => s)
                .Select(s => s.ToString());
            txtSelectedInfo.Text = string.Join(" ", seqs);
        }

        private void TriggerShowOnlySelectedInTekla()
        {
            if (dgvRebar.SelectedItems == null) return;
            var allItems = ViewModel.Groups.ToList();
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();

            string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
            string macroDir = GetMacroDirectory();

            string templatePath = Path.Combine(appFolder, "Macro", "RedrawView.cs");
            string macroContent = File.ReadAllText(templatePath);
            string tempMacroName = "Temp_Run_RedrawView.cs";
            string tempRunPath = Path.Combine(macroDir, tempMacroName);
            File.WriteAllText(tempRunPath, macroContent);
            Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName);

            // Identify unselected items to hide
            var itemsToHide = allItems.Except(selectedItems).ToList();

            // Select items to hide so the macro can act on them
            ViewModel.SelectInModel(itemsToHide);

            try
            {
                string templatePath2 = Path.Combine(appFolder, "Macro", "HideElement.cs");
                string macroContent2 = File.ReadAllText(templatePath2);
                string tempMacroName2 = "Temp_Run_HideElement.cs";
                string tempRunPath2 = Path.Combine(macroDir, tempMacroName2);
                File.WriteAllText(tempRunPath2, macroContent2);
                Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName2);
            }
            catch { }

            // Restore original selection
            ViewModel.SelectInModel(selectedItems);
        }
        private string GetMacroDirectory()
        {
            string macroDir = string.Empty;
            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroDir);
            if (string.IsNullOrEmpty(macroDir)) return string.Empty;
            if (macroDir.Contains(";")) macroDir = macroDir.Split(';')[0];

            string drawingMacroPath = Path.Combine(macroDir, "drawings");
            if (!Directory.Exists(drawingMacroPath)) Directory.CreateDirectory(drawingMacroPath);

            return drawingMacroPath;
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
