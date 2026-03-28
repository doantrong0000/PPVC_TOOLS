using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Drawing
{
    /// <summary>
    ///     Create Drawing command
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class CreateDrawingCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Create Drawing - Coming soon!");
        }
    }
}
