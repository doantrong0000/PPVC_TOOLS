using Autodesk.Revit.Attributes;
using Nice3point.Revit.Toolkit.External;

namespace PPVCREVIT.Commands.Rebar
{
    /// <summary>
    ///     Rebar Schedule command - generate rebar schedule
    /// </summary>
    [UsedImplicitly]
    [Transaction(TransactionMode.Manual)]
    public class RebarScheduleCommand : ExternalCommand
    {
        public override void Execute()
        {
            Autodesk.Revit.UI.TaskDialog.Show("PPVC Tools", "Rebar Schedule - Coming soon!");
        }
    }
}
