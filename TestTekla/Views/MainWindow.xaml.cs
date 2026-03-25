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


        private void BtnOpeningX_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                _viewModel.DrawOpeningDiagonal();
            }
            finally
            {
                this.Show();
            }
        }
        private void BtnAutoOpeningX_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                int count = _viewModel.AutoDrawOpeningDiagonals();
                if (count > 0)
                {
                    System.Windows.MessageBox.Show($"Đã vẽ đường chéo cho {count} lỗ mở.", "Auto Opening ✕", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    System.Windows.MessageBox.Show("Không tìm thấy lỗ mở nào trong drawing hiện tại.", "Auto Opening ✕", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            finally
            {
                this.Show();
            }
        }
        private void BtnDeleteById_Click(object sender, RoutedEventArgs e)
        {
            string idInput = txtDeleteId.Text;
            if (string.IsNullOrWhiteSpace(idInput))
            {
                MessageBox.Show("Please enter an ID or GUID to delete.");
                return;
            }

            bool success = _viewModel.DeleteObjectById(idInput);
            if (success)
            {
                MessageBox.Show($"Successfully deleted object: {idInput}");
                txtDeleteId.Text = string.Empty;
            }
            else
            {
                MessageBox.Show($"Could not find or delete object with ID: {idInput}");
            }
        }
    }
}
