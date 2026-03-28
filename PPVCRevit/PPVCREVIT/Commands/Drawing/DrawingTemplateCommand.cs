using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Drawing
{
    /// <summary>
    ///     Drawing Template command - manage drawing templates
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class DrawingTemplateCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Drawing Template - Coming soon!");
        }
    }
}
