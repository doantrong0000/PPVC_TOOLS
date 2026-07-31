using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class RevitClass
    {
        public static Document Doc { get; set; }

        // Có thể thêm các thuộc tính dùng chung khác
        public static UIDocument UiDoc { get; set; }
        public static UIApplication UiApp { get; set; }

        // Lưu trữ tâm của BoundingBox PPVC
        public static XYZ PPVCCenter { get; set; }

        public static void Model(ExternalCommandData commandData)
        {
            UiApp = commandData.Application;
            UiDoc = UiApp.ActiveUIDocument;
            Doc = UiDoc.Document;
        }
    }
}
