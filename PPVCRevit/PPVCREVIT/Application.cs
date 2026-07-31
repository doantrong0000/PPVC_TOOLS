using System;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI;
using Nice3point.Revit.Toolkit.External;
using PPVCREVIT.Commands.Model;
using PPVCREVIT.Commands.Drawing;
using PPVCREVIT.Commands.Drawing.RebarSchedule;

namespace PPVCREVIT
{
    /// <summary>
    /// Application entry point - creates PPVC Tools ribbon tab
    /// </summary>
    // Đổi tên class thành App hoặc RibbonApp để tránh xung đột với từ khóa Application của hệ thống/toolkit
    public class Application : ExternalApplication
    {
        public override void OnStartup()
        {
            CreateRibbon();
        }
        public override void OnShutdown()
        {
        }


        private void CreateRibbon()
        {
            string tabName = "PPVCREVIT";

            // Trong Nice3point Toolkit, sử dụng thuộc tính 'Context' hoặc tạo trực tiếp từ UiApplication
            // Cách an toàn và chuẩn nhất để tạo Tab/Panel thông qua UIApplication của toolkit:
            try
            {
                UiApplication.CreateRibbonTab(tabName);
            }
            catch (Autodesk.Revit.Exceptions.ArgumentException)
            {
                // Tab đã tồn tại
            }

            // Tạo Panel dựa trên tabName vừa khai báo
            RibbonPanel modelPanel = UiApplication.CreateRibbonPanel(tabName, "Model");

            string assemblyPath = typeof(Application).Assembly.Location;

            PushButtonData buttonData = new PushButtonData(
                "FindCOGCommand",
                "PPVC-COG",
                assemblyPath,
                typeof(FindCOGCommand).FullName)
            {
                ToolTip = "Find Center of Gravity"
            };

            Uri iconUri = new Uri("pack://application:,,,/PPVCREVIT;component/Resources/Icons/room16.png", UriKind.Absolute);
            buttonData.LargeImage = new BitmapImage(iconUri);

            modelPanel.AddItem(buttonData);

            PushButtonData buttonWithoutRebarData = new PushButtonData(
                "FindCOGWithoutRebarCommand",
                "PPVC-COG\n(No Rebar)",
                assemblyPath,
                typeof(FindCOGofPartCommand).FullName)
            {
                ToolTip = "Find Center of Gravity without calculating Rebar weight and centroid",
                LargeImage = new BitmapImage(iconUri)
            };

            modelPanel.AddItem(buttonWithoutRebarData);

            PushButtonData loadSharedParamData = new PushButtonData(
                "LoadSharedParameterCommand",
                "Load Rebar Params",
                assemblyPath,
                typeof(LoadSharedParameterCommand).FullName)
            {
                ToolTip = "Load Shared Parameters for Structural Rebar",
                LargeImage = new BitmapImage(iconUri)
            };
            modelPanel.AddItem(loadSharedParamData);



            RibbonPanel drawingPanel = UiApplication.CreateRibbonPanel(tabName, "Drawing");

            PushButtonData floorStepData = new PushButtonData(
                "CreateFloorStepCommand",
                "Create Floor Step",
                assemblyPath,
                typeof(CreateFloorStepCommand).FullName)
            {
                ToolTip = "Create Floor Step (Host)",
                Image = new BitmapImage(iconUri)
            };

            PushButtonData floorStepLinkData = new PushButtonData(
                "CreateFloorStepLinkCommand",
                "Create Floor Step Link",
                assemblyPath,
                typeof(CreateFloorStepLinkCommand).FullName)
            {
                ToolTip = "Create Floor Step (Link)",
                Image = new BitmapImage(iconUri)
            };

            PushButtonData tagFloorData = new PushButtonData(
                "TagFloorCommand",
                "Tag Floor",
                assemblyPath,
                typeof(TagFloorCommand).FullName)
            {
                ToolTip = "Tag Floor Thickness (Host)",
                Image = new BitmapImage(iconUri)
            };

            PushButtonData tagFloorLinkData = new PushButtonData(
                "TagFloorLinkCommand",
                "Tag Floor Link",
                assemblyPath,
                typeof(TagFloorLinkCommand).FullName)
            {
                ToolTip = "Tag Floor Thickness (Link)",
                Image = new BitmapImage(iconUri)
            };

            drawingPanel.AddStackedItems(floorStepData, floorStepLinkData);
            drawingPanel.AddStackedItems(tagFloorData, tagFloorLinkData);

            PushButtonData createPPVCSectionData = new PushButtonData(
                "CreatePPVCCommand",
                "Create PPVC",
                assemblyPath,
                typeof(PPVCCommand).FullName)
            {
                ToolTip = "Tạo 4 hướng nhìn elevation cho cấu kiện",
                LargeImage = new BitmapImage(iconUri)
            };
            drawingPanel.AddItem(createPPVCSectionData);

            PushButtonData rebarScheduleData = new PushButtonData(
                "RebarScheduleCommand",
                "Rebar Schedule",
                assemblyPath,
                typeof(RebarScheduleCommand).FullName)
            {
                ToolTip = "Check visible rebars and their tag counts in active view",
                LargeImage = new BitmapImage(iconUri)
            };
            drawingPanel.AddItem(rebarScheduleData);
        }
    }
}