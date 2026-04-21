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
