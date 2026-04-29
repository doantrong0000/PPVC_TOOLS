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

            // Auto-detect: if a drawing is open, switch to Drawing view
            try
            {
                var dh = new Tekla.Structures.Drawing.DrawingHandler();
                if (dh.GetActiveDrawing() != null)
                {
                    rbDrawing.IsChecked = true;
                }
            }
            catch { /* Not in drawing mode, stay on Model */ }
        }

        private void Mode_Checked(object sender, RoutedEventArgs e)
        {
            if (pnlModelMenu == null || pnlDrawingMenu == null) return;

            if (rbModel?.IsChecked == true)
            {
                pnlModelMenu.Visibility = Visibility.Visible;
                pnlDrawingMenu.Visibility = Visibility.Collapsed;
                if (MainContentControl != null) MainContentControl.Content = null;
            }
            else if (rbDrawing?.IsChecked == true)
            {
                pnlModelMenu.Visibility = Visibility.Collapsed;
                pnlDrawingMenu.Visibility = Visibility.Visible;
                if (MainContentControl != null) MainContentControl.Content = null;
            }
        }

        private void BtnStepTag_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                _viewModel.RunStepTag();
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnRebarTagScanner_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new DrawingRebarTagScannerPage();
        }

        private void BtnRebarTools_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new RebarToolsPage();
        }

        private void BtnRebarRSQN_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new RebarRSQNPage();
        }

        private void BtnCastUnitTools_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new CastUnitToolsPage();
        }

        private void BtnPPVCAutoDimTag_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new PPVCAutoDimTagPage();
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
