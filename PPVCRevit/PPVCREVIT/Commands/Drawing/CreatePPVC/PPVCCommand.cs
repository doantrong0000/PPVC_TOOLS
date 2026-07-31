using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Commands.Drawing.CreatePPVC;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Models;
using PPVCREVIT.Commands.Drawing.CreatePPVC.ViewModels;
using PPVCREVIT.Commands.Drawing.CreatePPVC.Views;
using PPVCREVIT.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Interop;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class PPVCCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                RevitClass.Model(commandData);

                PPVCView view = new PPVCView();
                
                var eventHandler = new PPVCEventHandler();
                var externalEvent = ExternalEvent.Create(eventHandler);

                PPVCViewModel vm = new PPVCViewModel(view, externalEvent, eventHandler);
                view.DataContext = vm;

                IntPtr revitHandle = commandData.Application.MainWindowHandle;
                if (revitHandle != IntPtr.Zero)
                {
                    var helper = new WindowInteropHelper(view);
                    helper.Owner = revitHandle;
                }

                view.Show();

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
