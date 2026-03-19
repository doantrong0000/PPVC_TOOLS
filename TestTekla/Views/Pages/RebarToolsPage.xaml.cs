using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;
using TeklaApp.ViewModels.PageModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarToolsPage : UserControl
    {
        private MainViewModel _viewModel;

        public RebarToolsPage()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
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
            double targetSpace;
            if (!double.TryParse(txtQuickRebarSpacing.Text, out targetSpace) || targetSpace <= 0)
            {
                MessageBox.Show("Please enter a valid spacing!", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            double startCover = 0;
            double.TryParse(txtStartCover.Text, out startCover);

            double endCover = 0;
            double.TryParse(txtEndCover.Text, out endCover);

            var vm = new CreateRebarViewModel();
            int zoneCount;
            string result = vm.CreateRebarWithMultiPoints(targetSpace, startCover, endCover, out zoneCount);
            
            if (!string.IsNullOrEmpty(result))
            {
                if (result.Contains("Error") || result.Contains("Cancelled"))
                    MessageBox.Show(result, "Multi-Pt Rebar", MessageBoxButton.OK, MessageBoxImage.Warning);
                else
                    MessageBox.Show(result, "Multi-Pt Rebar", MessageBoxButton.OK, MessageBoxImage.Information);
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
