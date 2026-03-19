using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class ViewAlignPage : UserControl
    {
        private ViewAlignViewModel _viewModel;

        public ViewAlignPage()
        {
            InitializeComponent();
            _viewModel = new ViewAlignViewModel();
        }

        private AlignAxis GetSelectedAxis()
        {
            if (rbAxisX.IsChecked == true) return AlignAxis.X;
            if (rbAxisY.IsChecked == true) return AlignAxis.Y;
            return AlignAxis.Z;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                btnRun.IsEnabled = false;
                txtStatus.Text = "ACTIVE: Waiting for Tekla pick...";

                AlignAxis axis = GetSelectedAxis();
                bool isMoveMode = (rbModeMoveObject.IsChecked == true);

                int alignedCount;
                string result;

                if (isMoveMode)
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
