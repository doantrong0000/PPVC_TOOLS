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
        }

        private void BtnRead_Click(object sender, RoutedEventArgs e)
        {
            Window parentWindow = Window.GetWindow(this);
            parentWindow.Hide();
            try
            {
                string result = _viewModel.ReadParameters();
                txtInfo.Text = result;
            }
            finally
            {
                parentWindow.Show();
            }
        }
    }
}
