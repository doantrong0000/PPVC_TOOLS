using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarNumberingPage : UserControl
    {
        private RebarNumberingViewModel _viewModel;

        public RebarNumberingPage()
        {
            InitializeComponent();
            _viewModel = new RebarNumberingViewModel();
            this.DataContext = _viewModel;
        }

        private void BtnRun_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RunNumbering();
        }

        private void BtnAutoPrefix_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.RunAutoPrefix();
        }
    }
}
