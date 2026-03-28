using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Drawing
{
    /// <summary>
    ///     Export Drawing command
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class ExportDrawingCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Export Drawing - Coming soon!");
        }
    }
}
