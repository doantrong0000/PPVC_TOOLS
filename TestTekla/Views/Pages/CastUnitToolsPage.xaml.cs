using System.Windows;
using System.Windows.Controls;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class CastUnitToolsPage : UserControl
    {
        private MainViewModel _viewModel;

        public CastUnitToolsPage()
        {
            InitializeComponent();
            _viewModel = new MainViewModel();
        }

        private void BtnAddPartsToCastUnit_Click(object sender, RoutedEventArgs e)
        {
            _viewModel.AddPartsToCastUnit();
        }
    }
}
