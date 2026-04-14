using System.Windows;
using TeklaApp.Helpers;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarSettingsWindow : Window
    {
        private RebarInspectorViewModel _viewModel;

        public RebarSettingsWindow(RebarInspectorViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            
            // Bind the datagrid to the view model's ObservableCollection
            dgSizeColor.ItemsSource = _viewModel.SizeColorTable;
            
            LoadSettings();
        }

        private void LoadSettings()
        {
            var settings = SettingsService.LoadSettings();
            chkUseExclusion.IsChecked = settings.UseExclusion;
            txtExcludeNames.Text = settings.ExcludeNames;
            txtSlabKeywords.Text = settings.SlabKeywords;
            txtBeamKeywords.Text = settings.BeamKeywords;
            txtWallKeywords.Text = settings.WallKeywords;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            var settings = SettingsService.LoadSettings();
            
            settings.UseExclusion = chkUseExclusion.IsChecked ?? true;
            settings.ExcludeNames = txtExcludeNames.Text;
            settings.SlabKeywords = txtSlabKeywords.Text;
            settings.BeamKeywords = txtBeamKeywords.Text;
            settings.WallKeywords = txtWallKeywords.Text;

            // Save settings via the ViewModel so it can update its own variables and the SizeClassMapping
            _viewModel.UpdateSettings(settings);
            
            this.DialogResult = true;
            this.Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}
