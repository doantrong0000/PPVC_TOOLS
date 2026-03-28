using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Model
{
    /// <summary>
    ///     Edit PPVC Module command
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class EditModuleCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Edit Module - Coming soon!");
        }
    }
}
