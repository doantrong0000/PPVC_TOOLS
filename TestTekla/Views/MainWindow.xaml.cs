using System;
using System.Windows;
using TeklaApp.ViewModels;
using TeklaApp.Views.Pages;

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

        private void BtnReverseRebar_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Reverse Rebar Distribution";
            _viewModel.ReverseRebarDistribution();
        }

        private void BtnRepickRebarRange_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Repick Rebar Range";
            _viewModel.RepickRebarRange();
        }

        private void BtnAdjustRebarLeg_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Measure Rebar Leg";
            _viewModel.CheckLap();
        }

        private void BtnSelectRebarsOfPart_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Select Part's Rebars";
            _viewModel.SelectRebarsOfPart();
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

        private void BtnRebarNumbering_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Rebar Numbering";
            MainContentControl.Content = new RebarNumberingPage();
        }

        private void BtnRebarInspector_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Rebar Inspector";
            MainContentControl.Content = new RebarInspectorPage();
        }
    }
}
