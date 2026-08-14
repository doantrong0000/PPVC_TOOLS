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

        private CreateRebarViewModel _createVm;

        public MainWindow()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
            _createVm = new CreateRebarViewModel();
            FindRebarPanel.DataContext = _createVm;

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

        private void BtnRebarTools_Click(object sender, RoutedEventArgs e)
        {
            MainContentControl.Content = new RebarToolsPage();
        }

        private RebarRSQNPage _rebarRSQNPage;
        private void BtnRebarRSQN_Click(object sender, RoutedEventArgs e)
        {
            if (_rebarRSQNPage == null)
            {
                _rebarRSQNPage = new RebarRSQNPage();
            }
            MainContentControl.Content = _rebarRSQNPage;
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

        private void BtnPartHatch_Click(object sender, RoutedEventArgs e)
        {
            this.Hide();
            try
            {
                _viewModel.DrawPartHatchPolygon();
            }
            finally
            {
                this.Show();
            }
        }

        private void BtnFindRebar_Click(object sender, RoutedEventArgs e)
        {
            _createVm.RunFindRebar();
        }

        private void BtnPickAssembly_Click(object sender, RoutedEventArgs e)
        {
            _createVm.PickAssembly();
        }

        private void TxtFindSeq_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                _createVm.RunFindRebar();
            }
        }
    }
}
