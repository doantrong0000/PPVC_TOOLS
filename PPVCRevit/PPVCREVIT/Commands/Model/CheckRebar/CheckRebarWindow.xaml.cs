using Autodesk.Revit.UI;
using System.Windows;
using System.Windows.Input;

namespace PPVCREVIT.Commands.Model.CheckRebar
{
    public partial class CheckRebarWindow : Window
    {
        private ExternalEvent _externalEvent;
        private CheckRebarEventHandler _eventHandler;

        public CheckRebarWindow()
        {
            InitializeComponent();
        }

        public void SetupEvent(ExternalEvent externalEvent, CheckRebarEventHandler eventHandler)
        {
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;
            _eventHandler.StatusCallback = UpdateStatus;
        }

        private void SelectButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteAction(CheckRebarAction.Select);
        }

        private void ShowOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            ExecuteAction(CheckRebarAction.SelectAndIsolate);
        }

        private void ExecuteAction(CheckRebarAction action)
        {
            string input = SearchTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrEmpty(input))
            {
                SearchTextBox.Focus();
                return;
            }

            if (_eventHandler != null && _externalEvent != null)
            {
                _eventHandler.SearchText = input;
                _eventHandler.CurrentAction = action;
                _externalEvent.Raise();
            }
        }

        private void SearchTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExecuteAction(CheckRebarAction.SelectAndIsolate);
            }
            else if (e.Key == Key.Escape)
            {
                Close();
            }
        }

        private void SearchTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            SearchTextBox.SelectAll();
        }

        public void UpdateStatus(string message, bool isSuccess)
        {
            // Empty callback implementation (Status text box removed as requested)
        }
    }
}
