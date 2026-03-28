//using Nice3point.Revit.Toolkit.External;
//using PPVCREVIT.Commands;

//namespace PPVCREVIT
//{
//    /// <summary>
//    ///     Application entry point
//    /// </summary>
//    [UsedImplicitly]
//    public class Application : ExternalApplication
//    {
//        public override void OnStartup()
//        {
//            CreateRibbon();
//        }

//        private void CreateRibbon()
//        {
//            var panel = Application.CreatePanel("Commands", "PPVCREVIT");

//            panel.AddPushButton<StartupCommand>("Execute")
//                .SetImage("/PPVCREVIT;component/Resources/Icons/RibbonIcon16.png")
//                .SetLargeImage("/PPVCREVIT;component/Resources/Icons/RibbonIcon32.png");
//        }
//    }
//}