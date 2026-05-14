using System;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using PPVCREVIT.Commands.Model;


namespace PPVCREVIT
{
    /// <summary>
    ///     Application entry point - creates PPVC Tools ribbon tab
    /// </summary>

    public class Application : ExternalApplication
    {
        private static readonly string AssemblyPath = typeof(Application).Assembly.Location;

        public override void OnStartup()
        {
            CreateRibbon();
        }

        private void CreateRibbon()
        {
            string tabName = "PPVCREVIT";
            try
            {
                Application.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab đã tồn tại
            }

            RibbonPanel modelPanel = Application.CreateRibbonPanel(tabName, "Model");

            string assemblyPath = typeof(Application).Assembly.Location;
            PushButtonData buttonData = new PushButtonData(
                "FindCOGCommand",
                "PPVC",
                assemblyPath,
                typeof(FindCOGCommand).FullName)
            {
                ToolTip = "Find Center of Gravity"
            };

            modelPanel.AddItem(buttonData);
        }
    }
}