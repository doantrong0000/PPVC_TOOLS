using System;
using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views
{
    public partial class ParameterPage : UserControl
    {
        private ParameterViewModel _viewModel;

        public ParameterPage()
        {
            InitializeComponent();
            _viewModel = new ParameterViewModel();
            this.DataContext = _viewModel;
        }

        private void BtnPick1_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.PickObject(1));
        private void BtnPick2_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.PickObject(2));

        private void BtnCopy1_Click(object sender, RoutedEventArgs e) => _viewModel.CopyField(1);
        private void BtnCopy2_Click(object sender, RoutedEventArgs e) => _viewModel.CopyField(2);
        private void BtnCopy3_Click(object sender, RoutedEventArgs e) => _viewModel.CopyField(3);
        private void BtnCopy4_Click(object sender, RoutedEventArgs e) => _viewModel.CopyField(4);
        private void BtnCopyAll_Click(object sender, RoutedEventArgs e) => _viewModel.CopyAll();

        private void BtnSave2_Click(object sender, RoutedEventArgs e) => _viewModel.StatusMessage = _viewModel.SaveToTarget();

        private void BtnApply1_Selected_Click(object sender, RoutedEventArgs e) => _viewModel.StatusMessage = _viewModel.ApplyToAllSelected(1);
        private void BtnApply1_Pick_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.StatusMessage = _viewModel.ApplyToSweepSelected(1));

        private void BtnApply2_Selected_Click(object sender, RoutedEventArgs e) => _viewModel.StatusMessage = _viewModel.ApplyToAllSelected(2);
        private void BtnApply2_Pick_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.StatusMessage = _viewModel.ApplyToSweepSelected(2));

        private void BtnApply3_Selected_Click(object sender, RoutedEventArgs e) => _viewModel.StatusMessage = _viewModel.ApplyToAllSelected(3);
        private void BtnApply3_Pick_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.StatusMessage = _viewModel.ApplyToSweepSelected(3));

        private void BtnApply4_Selected_Click(object sender, RoutedEventArgs e) => _viewModel.StatusMessage = _viewModel.ApplyToAllSelected(4);
        private void BtnApply4_Pick_Click(object sender, RoutedEventArgs e) => RunInTekla(() => _viewModel.StatusMessage = _viewModel.ApplyToSweepSelected(4));

        private void RunInTekla(Action action)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow.Hide();
            try { action(); }
            finally { parentWindow.Show(); }
        }
    }
}
