using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Interop;

namespace PPVCREVIT.Commands.Drawing.RebarSchedule
{
    [Transaction(TransactionMode.Manual)]
    public class RebarScheduleCommand : IExternalCommand
    {
        private static RebarScheduleWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // If window is already open, bring it to focus
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                _window = new RebarScheduleWindow();

                // Set up event handler and external event for modeless operations
                var eventHandler = new RebarScheduleEventHandler(_window);
                var externalEvent = ExternalEvent.Create(eventHandler);
                _window.SetupEvent(externalEvent, eventHandler);

                // Set Revit window as the owner of the WPF window so it behaves correctly
                IntPtr revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(_window);
                    helper.Owner = revitHandle;
                }

                _window.Show();

                // Trigger initial fetch of visible rebars
                _window.TriggerFetch();

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                message = ex.Message;
                return Result.Failed;
            }
        }
    }
}
