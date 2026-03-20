using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;
using TeklaApp.Helpers;
using TeklaApp.Models;

namespace TeklaApp.Views.Pages
{
    public partial class ViewAlignPage : UserControl
    {
        private ViewAlignViewModel _viewModel;

        public ViewAlignPage()
        {
            InitializeComponent();
            _viewModel = new ViewAlignViewModel();
            LoadPersistentSettings();
        }

        private void LoadPersistentSettings()
        {
            var settings = SettingsService.LoadSettings();

            if (settings.AlignAxis == "X") rbAxisX.IsChecked = true;
            else if (settings.AlignAxis == "Y") rbAxisY.IsChecked = true;
            else rbAxisZ.IsChecked = true;

            if (settings.AlignMode == "MoveObject") rbModeMoveObject.IsChecked = true;
            else if (settings.AlignMode == "DrawingView") rbModeDrawingView.IsChecked = true;
            else rbModeEditPoints.IsChecked = true;

            var chkAlignByCenter = this.FindName("chkAlignByCenter") as CheckBox;
            if (chkAlignByCenter != null) chkAlignByCenter.IsChecked = settings.AlignByCenter;
        }

        private void SavePersistentSettings()
        {
            var settings = SettingsService.LoadSettings();

            if (rbAxisX.IsChecked == true) settings.AlignAxis = "X";
            else if (rbAxisY.IsChecked == true) settings.AlignAxis = "Y";
            else settings.AlignAxis = "Z";

            if (rbModeMoveObject.IsChecked == true) settings.AlignMode = "MoveObject";
            else if (rbModeDrawingView.IsChecked == true) settings.AlignMode = "DrawingView";
            else settings.AlignMode = "EditPoints";

            var chkAlignByCenter = this.FindName("chkAlignByCenter") as CheckBox;
            if (chkAlignByCenter != null) settings.AlignByCenter = (chkAlignByCenter.IsChecked == true);

            SettingsService.SaveSettings(settings);
        }

        private AlignAxis GetSelectedAxis()
        {
            if (rbAxisX.IsChecked == true) return AlignAxis.X;
            if (rbAxisY.IsChecked == true) return AlignAxis.Y;
            return AlignAxis.Z;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            SavePersistentSettings();
            try
            {
                btnRun.IsEnabled = false;
                txtStatus.Text = "ACTIVE: Waiting for Tekla pick...";

                AlignAxis axis = GetSelectedAxis();
                bool isMoveMode = (rbModeMoveObject.IsChecked == true);
                bool isDrawingViewMode = (rbModeDrawingView.IsChecked == true);

                int alignedCount;
                string result;

                if (isDrawingViewMode)
                {
                    var chkAlignByCenter = this.FindName("chkAlignByCenter") as CheckBox;
                    bool useCenter = (chkAlignByCenter != null && chkAlignByCenter.IsChecked == true);
                    result = _viewModel.AlignDrawingViews(axis, useCenter, out alignedCount);
                }
                else if (isMoveMode)
                {
                    result = _viewModel.AlignByMovingObjects(axis, out alignedCount);
                }
                else
                {
                    result = _viewModel.AlignObjectPoints(axis, out alignedCount);
                }

                // Update result log
                if (_viewModel.ReferencePoint != null)
                {
                    var rp = _viewModel.ReferencePoint;
                    txtRefPoint.Text = $"({rp.X:F1}, {rp.Y:F1}, {rp.Z:F1})";
                }
                else
                {
                    txtRefPoint.Text = "—";
                }

                txtAlignedCount.Text = alignedCount > 0 ? alignedCount.ToString() : "—";
                txtResultMessage.Text = result;

                // Update status bar
                txtStatus.Text = alignedCount > 0
                    ? $"Done: {alignedCount} object(s) aligned on {axis}"
                    : "Ready";
            }
            catch (Exception ex)
            {
                txtStatus.Text = "Error: " + ex.Message;
                txtResultMessage.Text = "Error: " + ex.Message;
            }
            finally
            {
                btnRun.IsEnabled = true;
            }
        }
    }
}
