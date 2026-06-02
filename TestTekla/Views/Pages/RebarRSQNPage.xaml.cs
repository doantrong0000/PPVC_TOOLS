using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Reflection;
using TeklaApp.ViewModels;

namespace TeklaApp.Views.Pages
{
    public partial class RebarRSQNPage : UserControl
    {
        public RebarRSQNViewModel ViewModel { get; }

        public ObservableCollection<FilterItem> FilterItems { get; set; } = new ObservableCollection<FilterItem>();
        private Dictionary<string, HashSet<string>> _activeFilters = new Dictionary<string, HashSet<string>>();
        private string _currentFilterColumnBinding;
        private ToggleButton _currentFilterButton;

        public RebarRSQNPage()
        {
            InitializeComponent();
            ViewModel = new RebarRSQNViewModel();
            this.DataContext = ViewModel;
            filterPopup.DataContext = this;
        }

        private void BtnGetRebar_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.GetRebarsFromModel();
            ViewModel.CheckRebar();
        }

        private void BtnSelectInModel_Click(object sender, RoutedEventArgs e)
        {
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            if (chkShowSelected.IsChecked == true)
            {
                TriggerShowOnlySelectedInTekla();
            }
            else
            {
                ViewModel.SelectInModel(selectedItems);
            }
        }


        private void DgvRebar_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (dgvRebar.SelectedItems == null) return;
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            var seqs = selectedItems
       .OrderBy(s => s.Seq)                     // sort by Seq (double)
       .Select(s => s.Seq.ToString())
       .Distinct();                             // or Distinct() after Select if you want unique displays

            txtSelectedInfo.Text = string.Join(" ", seqs);
        }

        private void TriggerShowOnlySelectedInTekla()
        {
            if (dgvRebar.SelectedItems == null) return;
            var allItems = ViewModel.Groups.ToList();
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();

            string appFolder = System.AppDomain.CurrentDomain.BaseDirectory;
            string macroDir = GetMacroDirectory();

            string templatePath = Path.Combine(appFolder, "Macro", "RedrawView.cs");
            string macroContent = File.ReadAllText(templatePath);
            string tempMacroName = "Temp_Run_RedrawView.cs";
            string tempRunPath = Path.Combine(macroDir, tempMacroName);
            File.WriteAllText(tempRunPath, macroContent);
            Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName);

            // Identify unselected items to hide
            var itemsToHide = allItems.Except(selectedItems).ToList();

            // Select items to hide so the macro can act on them
            ViewModel.SelectInModel(itemsToHide);

            try
            {
                string templatePath2 = Path.Combine(appFolder, "Macro", "HideElement.cs");
                string macroContent2 = File.ReadAllText(templatePath2);
                string tempMacroName2 = "Temp_Run_HideElement.cs";
                string tempRunPath2 = Path.Combine(macroDir, tempMacroName2);
                File.WriteAllText(tempRunPath2, macroContent2);
                Tekla.Structures.Model.Operations.Operation.RunMacro(@"..\drawings\" + tempMacroName2);
            }
            catch { }

            // Restore original selection
            ViewModel.SelectInModel(selectedItems);
        }
        private string GetMacroDirectory()
        {
            string macroDir = string.Empty;
            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroDir);
            if (string.IsNullOrEmpty(macroDir)) return string.Empty;
            if (macroDir.Contains(";")) macroDir = macroDir.Split(';')[0];

            string drawingMacroPath = Path.Combine(macroDir, "drawings");
            if (!Directory.Exists(drawingMacroPath)) Directory.CreateDirectory(drawingMacroPath);

            return drawingMacroPath;
        }

        private void DgvRebar_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var selectedItems = dgvRebar.SelectedItems.Cast<RSQNGroupItem>().ToList();
            ViewModel.SelectInModel(selectedItems);
        }

        private void CHECK_Click(object sender, RoutedEventArgs e)
        {
            _activeFilters.Clear();
            ApplyFilters();
            ViewModel.CheckRebar();
        }

        private T FindVisualParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parentObject = System.Windows.Media.VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            T parent = parentObject as T;
            if (parent != null) return parent;
            return FindVisualParent<T>(parentObject);
        }

        private void BtnFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is ToggleButton btn)) return;

            var header = FindVisualParent<DataGridColumnHeader>(btn);
            if (header == null) return;

            var column = header.Column as DataGridBoundColumn;
            if (column == null || !(column.Binding is Binding binding))
            {
                btn.IsChecked = false;
                return;
            }

            _currentFilterColumnBinding = binding.Path.Path;
            _currentFilterButton = btn;

            // Populate distinct values
            FilterItems.Clear();
            var allItems = ViewModel.Groups;
            var distinctValues = new HashSet<string>();

            PropertyInfo propInfo = typeof(RSQNGroupItem).GetProperty(_currentFilterColumnBinding);
            if (propInfo != null)
            {
                foreach (var item in allItems)
                {
                    var val = propInfo.GetValue(item)?.ToString() ?? "";
                    distinctValues.Add(val);
                }
            }

            var activeFilterForColumn = _activeFilters.ContainsKey(_currentFilterColumnBinding)
                ? _activeFilters[_currentFilterColumnBinding]
                : null;

            foreach (var val in distinctValues.OrderBy(v => v))
            {
                bool isSelected = activeFilterForColumn == null || activeFilterForColumn.Contains(val);
                FilterItems.Add(new FilterItem { Value = val, IsSelected = isSelected });
            }

            txtFilterSearch.Text = "";
            UpdateSelectAllCheckboxState();

            // Set DataContext for the ItemsSource if needed (handled in constructor)
            // Position and open popup
            filterPopup.PlacementTarget = btn;
            filterPopup.IsOpen = true;
        }

        private void TxtFilterSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var searchText = txtFilterSearch.Text.ToLower();
            ICollectionView view = CollectionViewSource.GetDefaultView(lstFilterItems.ItemsSource);
            view.Filter = obj =>
            {
                var item = obj as FilterItem;
                if (item == null) return false;
                return item.DisplayValue.ToLower().Contains(searchText);
            };
        }

        private void ChkSelectAllFilter_Click(object sender, RoutedEventArgs e)
        {
            bool isChecked = chkSelectAllFilter.IsChecked == true;
            ICollectionView view = CollectionViewSource.GetDefaultView(lstFilterItems.ItemsSource);
            foreach (FilterItem item in view)
            {
                item.IsSelected = isChecked;
            }
        }

        private void FilterItemCheckBox_Click(object sender, RoutedEventArgs e)
        {
            UpdateSelectAllCheckboxState();
        }

        private void UpdateSelectAllCheckboxState()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(lstFilterItems.ItemsSource);
            bool allSelected = true;
            bool noneSelected = true;
            foreach (FilterItem item in view)
            {
                if (item.IsSelected) noneSelected = false;
                else allSelected = false;
            }

            if (allSelected) chkSelectAllFilter.IsChecked = true;
            else if (noneSelected) chkSelectAllFilter.IsChecked = false;
            else chkSelectAllFilter.IsChecked = null;
        }

        private void BtnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            var selectedValues = new HashSet<string>();
            foreach (var item in FilterItems)
            {
                if (item.IsSelected) selectedValues.Add(item.Value);
            }

            if (selectedValues.Count == FilterItems.Count)
            {
                _activeFilters.Remove(_currentFilterColumnBinding);
                if (_currentFilterButton != null) _currentFilterButton.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(102, 102, 102));
            }
            else
            {
                _activeFilters[_currentFilterColumnBinding] = selectedValues;
                if (_currentFilterButton != null) _currentFilterButton.Foreground = System.Windows.Media.Brushes.Red;
            }

            ApplyFilters();

            filterPopup.IsOpen = false;
            if (_currentFilterButton != null) _currentFilterButton.IsChecked = false;
        }

        private void BtnCancelFilter_Click(object sender, RoutedEventArgs e)
        {
            filterPopup.IsOpen = false;
            if (_currentFilterButton != null) _currentFilterButton.IsChecked = false;
        }

        private void ApplyFilters()
        {
            ICollectionView view = CollectionViewSource.GetDefaultView(ViewModel.Groups);
            if (_activeFilters.Count == 0)
            {
                view.Filter = null;
            }
            else
            {
                view.Filter = obj =>
                {
                    var item = obj as RSQNGroupItem;
                    if (item == null) return false;

                    foreach (var filter in _activeFilters)
                    {
                        var propInfo = typeof(RSQNGroupItem).GetProperty(filter.Key);
                        if (propInfo == null) continue;
                        var val = propInfo.GetValue(item)?.ToString() ?? "";
                        if (!filter.Value.Contains(val)) return false;
                    }
                    return true;
                };
            }
        }

        private void BtnSelectAll_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.RunFindRebar(txtSelectedInfo.Text);
        }
    }

    public class FilterItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(nameof(IsSelected)); }
        }

        public string Value { get; set; }
        public string DisplayValue => string.IsNullOrEmpty(Value) ? "(Blanks)" : Value;

        private bool _isVisible = true;
        public bool IsVisible
        {
            get => _isVisible;
            set { _isVisible = value; OnPropertyChanged(nameof(IsVisible)); }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}
