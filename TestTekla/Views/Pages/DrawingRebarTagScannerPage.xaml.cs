using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels.PageModels;

namespace TeklaApp.Views.Pages
{
    public partial class DrawingRebarTagScannerPage : UserControl
    {
        private DrawingRebarTagScannerViewModel _viewModel;

        public DrawingRebarTagScannerPage()
        {
            InitializeComponent();
            _viewModel = new DrawingRebarTagScannerViewModel();
            this.DataContext = _viewModel;
        }

        private void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                txtStatus.Text = "Scanning...";
                _viewModel.ScanActiveDrawing(out string status);
                txtStatus.Text = status;
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
            }
        }

        private void BtnExport_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.ExportToJson();
        }
    }
}
