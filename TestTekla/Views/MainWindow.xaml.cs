using System;
using System.Windows;
using TeklaApp.ViewModels;

namespace TeklaApp.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
        }

        private void BtnReadParams_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Parameters Explorer";
            MainContentControl.Content = new ParameterPage();
        }

        private void BtnDeleteCut_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "PartCuts Manager";
            string result = _viewModel.DeletePartCuts();
        }

        private void BtnQuickDim_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Quick Dimension";
            _viewModel.QuickDim();
        }

        private void BtnAddAssembly_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Assembly Joiner";
            MainContentControl.Content = new JoinAssemblyPage();
        }



        private void BtnStepTag_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Step Tag Generator (Drawing)";
            MainContentControl.Content = new StepTagPage();
        }
    }
}
