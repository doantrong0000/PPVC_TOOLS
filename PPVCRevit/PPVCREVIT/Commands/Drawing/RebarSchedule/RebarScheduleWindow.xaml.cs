using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.RebarSchedule.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;

namespace PPVCREVIT.Commands.Drawing.RebarSchedule
{
    public partial class RebarScheduleWindow : Window
    {
        private ExternalEvent? _externalEvent;
        private RebarScheduleEventHandler? _eventHandler;
        private ObservableCollection<RebarModel> _rebars;

        public RebarScheduleWindow()
        {
            InitializeComponent();
            _rebars = new ObservableCollection<RebarModel>();
            RebarsDataGrid.ItemsSource = _rebars;
        }

        public void SetupEvent(ExternalEvent externalEvent, RebarScheduleEventHandler eventHandler)
        {
            _externalEvent = externalEvent;
            _eventHandler = eventHandler;
        }

        public void TriggerFetch()
        {
            if (_eventHandler != null && _externalEvent != null)
            {
                _eventHandler.RequestType = RebarActionType.Fetch;
                _externalEvent.Raise();
            }
        }

        public void UpdateRebarList(List<RebarModel> list)
        {
            _rebars.Clear();
            foreach (var item in list)
            {
                _rebars.Add(item);
            }
        }

        private void GetRebarsButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerFetch();
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            TriggerFetch();
        }

        private void RebarsDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (RebarsDataGrid.SelectedItem is RebarModel selectedRebar)
            {
                if (_eventHandler != null && _externalEvent != null)
                {
                    _eventHandler.RequestType = RebarActionType.Select;
                    _eventHandler.SelectedRebarUniqueId = selectedRebar.Id;
                    _externalEvent.Raise();
                }
            }
        }
    }
}
