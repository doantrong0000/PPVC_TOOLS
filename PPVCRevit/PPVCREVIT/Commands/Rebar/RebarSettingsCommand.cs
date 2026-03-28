using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Rebar
{
    /// <summary>
    ///     Rebar Settings command
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class RebarSettingsCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Rebar Settings - Coming soon!");
        }
    }
}
