using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Windows.Interop;

namespace PPVCREVIT.Commands.Model.CheckRebar
{
    [Transaction(TransactionMode.Manual)]
    public class CheckRebarCommand : IExternalCommand
    {
        private static CheckRebarWindow _window;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                // Reactivate window if already open
                if (_window != null && _window.IsLoaded)
                {
                    _window.Activate();
                    return Result.Succeeded;
                }

                _window = new CheckRebarWindow();

                var eventHandler = new CheckRebarEventHandler();
                var externalEvent = ExternalEvent.Create(eventHandler);
                _window.SetupEvent(externalEvent, eventHandler);

                // Set Revit window handle as owner of WPF window
                IntPtr revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(_window);
                    helper.Owner = revitHandle;
                }

                _window.Show();

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
