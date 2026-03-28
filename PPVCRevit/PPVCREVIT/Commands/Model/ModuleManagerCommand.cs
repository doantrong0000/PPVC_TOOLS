using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Model
{
    /// <summary>
    ///     Module Manager command - manage module list
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class ModuleManagerCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Module Manager - Coming soon!");
        }
    }
}
