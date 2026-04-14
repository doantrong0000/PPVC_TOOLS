//using System;
//using System.Windows.Media.Imaging;
//using Autodesk.Revit.UI;
//using Nice3point.Revit.Toolkit.External;
//using PPVCREVIT.Commands.Model;
//using PPVCREVIT.Commands.Rebar;
//using PPVCREVIT.Commands.Drawing;

//namespace PPVCREVIT
//{
//    /// <summary>
//    ///     Application entry point - creates PPVC Tools ribbon tab
//    /// </summary>
//    [UsedImplicitly]
//    public class Application : ExternalApplication
//    {
//        private static readonly string AssemblyPath = typeof(Application).Assembly.Location;

//        public override void OnStartup()
//        {
//            CreateRibbon();
//        }

//        private void CreateRibbon()
//        {
//            // ═══════════════════════════════════════════════════════
//            // Panel 1: MODEL — Large: Create Module | Stacked: Edit + Manager
//            // ═══════════════════════════════════════════════════════
//            var modelPanel = Application.CreatePanel("Model", "PPVCREVIT");

//            modelPanel.AddPushButton<CreateModuleCommand>("Create\nModule")
//                .SetImage("/PPVCREVIT;component/Resources/Icons/CreateModule16.png")
//                .SetLargeImage("/PPVCREVIT;component/Resources/Icons/CreateModule32.png")
//                .SetToolTip("Create a new PPVC Module");

//            var editModuleData = CreateButtonData<EditModuleCommand>(
//                "EditModule", "Edit Module", "EditModule16.png", "Edit an existing PPVC Module");
//            var moduleManagerData = CreateButtonData<ModuleManagerCommand>(
//                "ModuleManager", "Module Manager", "ModuleManager16.png", "Manage PPVC Module list");
//            modelPanel.AddStackedItems(editModuleData, moduleManagerData);

//            // ═══════════════════════════════════════════════════════
//            // Panel 2: REBAR — Large: Place Rebar | Stacked: Schedule + Settings
//            // ═══════════════════════════════════════════════════════
//            var rebarPanel = Application.CreatePanel("Rebar", "PPVCREVIT");

//            rebarPanel.AddPushButton<PlaceRebarCommand>("Place\nRebar")
//                .SetImage("/PPVCREVIT;component/Resources/Icons/PlaceRebar16.png")
//                .SetLargeImage("/PPVCREVIT;component/Resources/Icons/PlaceRebar32.png")
//                .SetToolTip("Place reinforcement bars");

//            var rebarScheduleData = CreateButtonData<RebarScheduleCommand>(
//                "RebarSchedule", "Rebar Schedule", "RebarSchedule16.png", "Generate rebar schedule");
//            var rebarSettingsData = CreateButtonData<RebarSettingsCommand>(
//                "RebarSettings", "Rebar Settings", "RebarSettings16.png", "Configure rebar settings");
//            rebarPanel.AddStackedItems(rebarScheduleData, rebarSettingsData);

//            // ═══════════════════════════════════════════════════════
//            // Panel 3: DRAWING — Large: Create Drawing | Stacked: Template + Export
//            // ═══════════════════════════════════════════════════════
//            var drawingPanel = Application.CreatePanel("Drawing", "PPVCREVIT");

//            drawingPanel.AddPushButton<CreateDrawingCommand>("Create\nDrawing")
//                .SetImage("/PPVCREVIT;component/Resources/Icons/CreateDrawing16.png")
//                .SetLargeImage("/PPVCREVIT;component/Resources/Icons/CreateDrawing32.png")
//                .SetToolTip("Create a new drawing");

//            var drawingTemplateData = CreateButtonData<DrawingTemplateCommand>(
//                "DrawingTemplate", "Drawing Template", "DrawingTemplate16.png", "Manage drawing templates");
//            var exportDrawingData = CreateButtonData<ExportDrawingCommand>(
//                "ExportDrawing", "Export Drawing", "ExportDrawing16.png", "Export drawings");
//            drawingPanel.AddStackedItems(drawingTemplateData, exportDrawingData);
//        }

//        /// <summary>
//        ///     Helper to create PushButtonData for stacked items
//        /// </summary>
//        private static PushButtonData CreateButtonData<TCommand>(
//            string internalName, string displayName, string iconFileName, string toolTip)
//            where TCommand : ExternalCommand
//        {
//            var data = new PushButtonData(
//                internalName,
//                displayName,
//                AssemblyPath,
//                typeof(TCommand).FullName)
//            {
//                Image = new BitmapImage(new Uri(
//                    $"/PPVCREVIT;component/Resources/Icons/{iconFileName}",
//                    UriKind.RelativeOrAbsolute)),
//                ToolTip = toolTip
//            };
//            return data;
//        }
//    }
//}