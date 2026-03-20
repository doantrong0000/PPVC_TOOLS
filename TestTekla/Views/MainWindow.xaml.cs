using System;
using System.Windows;
using TeklaApp.ViewModels;
using TeklaApp.ViewModels.PageModels;
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

        private void BtnDeleteCut_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "PartCuts Manager";
            _viewModel.DeletePartCuts();
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

        private void BtnRebarTools_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "Rebar Tools 🌟";
            MainContentControl.Content = new RebarToolsPage();
        }

        private void BtnViewAlign_Click(object sender, RoutedEventArgs e)
        {
            txtCurrentTool.Text = "View Align";
            MainContentControl.Content = new ViewAlignPage();
        }


    }
}
