using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;

namespace PPVCREVIT.Commands.Drawing.CreatePPVC.Models
{
    public static class CreateSectionModel
    {
        public static void CreateAllViewForPPVC(string namePPVC, string nameProject, string nameLevel)
        {
            ViewPlan viewPlan = RevitClass.UiDoc.ActiveView as ViewPlan;
            List<ViewSection> listView = new List<ViewSection>();

            // 1. Lấy ViewFamilyType phù hợp cho Elevation
            ViewFamilyType elevationViewFamilyType = new FilteredElementCollector(RevitClass.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Elevation);

            if (elevationViewFamilyType == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy ViewFamilyType phù hợp cho Elevation.");
                return;
            }

            // 1b. Lấy ViewFamilyType phù hợp cho Section
            ViewFamilyType sectionViewFamilyType = new FilteredElementCollector(RevitClass.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.Section);

            if (sectionViewFamilyType == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy ViewFamilyType phù hợp cho Section.");
                return;
            }

            // 2. Chọn cấu kiện: ưu tiên selection có sẵn, nếu chưa chọn thì bắt quét chọn
            List<Element> selectedElements = new List<Element>();
            ICollection<ElementId> preSelected = RevitClass.UiDoc.Selection.GetElementIds();
            if (preSelected != null && preSelected.Count > 0)
            {
                foreach (ElementId id in preSelected)
                {
                    Element el = RevitClass.Doc.GetElement(id);
                    if (el != null)
                        selectedElements.Add(el);
                }
            }
            else
            {
                try
                {
                    IList<Reference> refs = RevitClass.UiDoc.Selection.PickObjects(
                        ObjectType.Element,
                        "Quét chọn các cấu kiện để tạo 4 hướng nhìn elevation"
                    );
                    foreach (Reference r in refs)
                    {
                        Element el = RevitClass.Doc.GetElement(r);
                        if (el != null)
                            selectedElements.Add(el);
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return;
                }
            }

            if (selectedElements.Count == 0)
            {
                return;
            }

            // 3. Tính toán BoundingBox bao phủ tất cả cấu kiện đã chọn (bỏ qua thép/rebar)
            BoundingBoxXYZ combinedBBox = null;
            foreach (Element el in selectedElements)
            {
                if (el is Autodesk.Revit.DB.Structure.Rebar ||
                    el is Autodesk.Revit.DB.Structure.RebarInSystem ||
                    (el.Category != null && el.Category.Id.Value == (int)BuiltInCategory.OST_Rebar))
                {
                    continue;
                }

                BoundingBoxXYZ bbox = el.get_BoundingBox(null);
                if (bbox == null) continue;

                if (combinedBBox == null)
                {
                    combinedBBox = new BoundingBoxXYZ
                    {
                        Min = bbox.Min,
                        Max = bbox.Max
                    };
                }
                else
                {
                    combinedBBox.Min = new XYZ(
                        Math.Min(combinedBBox.Min.X, bbox.Min.X),
                        Math.Min(combinedBBox.Min.Y, bbox.Min.Y),
                        Math.Min(combinedBBox.Min.Z, bbox.Min.Z)
                    );
                    combinedBBox.Max = new XYZ(
                        Math.Max(combinedBBox.Max.X, bbox.Max.X),
                        Math.Max(combinedBBox.Max.Y, bbox.Max.Y),
                        Math.Max(combinedBBox.Max.Z, bbox.Max.Z)
                    );
                }
            }

            if (combinedBBox == null)
            {
                TaskDialog.Show("Lỗi", "Không tìm thấy cấu kiện có BoundingBox hợp lệ.");
                return;
            }

            // Lưu trữ tâm của cấu kiện PPVC vào biến dùng chung
            RevitClass.PPVCCenter = (combinedBBox.Min + combinedBBox.Max) / 2.0;

            // 4. Tạo các view tương ứng
            using (Transaction tx = new Transaction(RevitClass.Doc, "Tạo Elevation và Mặt cắt PPVC"))
            {
                tx.Start();

                // Tạo các view Elevation và Section (Bê tông & Thép)
                CreateElevationAndSectionViews(viewPlan, elevationViewFamilyType, sectionViewFamilyType, combinedBBox, listView, nameLevel);

                // Tạo các view Plan
                CreatePlanViews(viewPlan, combinedBBox, nameLevel, nameProject, namePPVC);

                // Gán các tham số chung cho các view elevation và section
                foreach (ViewSection v in listView)
                {
                    PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(v, "Level", nameLevel);
                    PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(v, "Project", nameProject);
                    PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(v, "PPVC", namePPVC);
                }

                tx.Commit();
            }
        }

        /// <summary>
        /// Tạo các view Elevation và Section cho cả phần Bê tông (Casscast) và phần Thép (Rebar).
        /// </summary>
        private static void CreateElevationAndSectionViews(
            ViewPlan viewPlan,
            ViewFamilyType elevationViewFamilyType,
            ViewFamilyType sectionViewFamilyType,
            BoundingBoxXYZ combinedBBox,
            List<ViewSection> listView, string nameLevel)
        {
            XYZ min = combinedBBox.Min;
            XYZ max = combinedBBox.Max;
            XYZ center = (min + max) / 2.0;

            // Tính toán khoảng cách offset thông minh (tối thiểu 3 feet hoặc 20% kích thước bao)
            double width = max.X - min.X;
            double depth = max.Y - min.Y;
            double size = Math.Max(width, depth);
            double offset = Math.Max(3.0, size * 0.2);

            #region tạo section và elevation cho casscast
            // Hướng nhìn từ Tây sang Đông (Marker đặt ở phía Tây nhìn về phía Đông - Index 0)
            XYZ posWest = new XYZ(max.X + offset, center.Y, center.Z);
            ElevationMarker markerWest = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posWest, 50);
            ViewSection viewEast = markerWest.CreateElevation(RevitClass.Doc, viewPlan.Id, 0);
            SetupView(viewEast, combinedBBox, GetUniqueViewName($"D ({nameLevel})"));
            SetTemplateForView(viewEast, "ELEVATION_VIEW");

            // Hướng nhìn từ Nam lên Bắc (Marker đặt ở phía Nam nhìn lên phía Bắc - Index 1)
            XYZ posSouth = new XYZ(center.X, min.Y - offset, center.Z);
            ElevationMarker markerSouth = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posSouth, 50);
            ViewSection viewNorth = markerSouth.CreateElevation(RevitClass.Doc, viewPlan.Id, 1);
            SetupView(viewNorth, combinedBBox, GetUniqueViewName($"A ({nameLevel})"));
            SetTemplateForView(viewNorth, "ELEVATION_VIEW");

            // Hướng nhìn từ Đông sang Tây (Marker đặt ở phía Đông nhìn về phía Tây - Index 2)
            XYZ posEast = new XYZ(min.X - offset, center.Y, center.Z);
            ElevationMarker markerEast = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posEast, 50);
            ViewSection viewWest = markerEast.CreateElevation(RevitClass.Doc, viewPlan.Id, 2);
            SetupView(viewWest, combinedBBox, GetUniqueViewName($"B ({nameLevel})"));
            SetTemplateForView(viewWest, "ELEVATION_VIEW");

            // Hướng nhìn từ Bắc xuống Nam (Marker đặt ở phía Bắc nhìn xuống phía Nam - Index 3)
            XYZ posNorth = new XYZ(center.X, max.Y + offset, center.Z);
            ElevationMarker markerNorth = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posNorth, 50);
            ViewSection viewSouth = markerNorth.CreateElevation(RevitClass.Doc, viewPlan.Id, 3);
            SetupView(viewSouth, combinedBBox, GetUniqueViewName($"C ({nameLevel})"));
            SetTemplateForView(viewSouth, "ELEVATION_VIEW");

            // --- TẠO 4 MẶT CẮT THEO YÊU CẦU ---
            double X1 = min.X + 0.25 * width;
            double X2 = min.X + 0.75 * width;
            double Y1 = min.Y + 0.25 * depth;
            double Y2 = min.Y + 0.75 * depth;
            double H = max.Z - min.Z;

            double secBuffer = 0.5; // feet

            var view1 = CreateSection(sectionViewFamilyType.Id,
                new XYZ(X1, center.Y, center.Z),
                new XYZ(0, 1, 0),
                new XYZ(0, 0, 1),
                new XYZ(1, 0, 0),
                depth + 2 * secBuffer,
                H + 2 * secBuffer,
               1,
                GetUniqueViewName($"SECTION 1 ({nameLevel})")
            );
            SetTemplateForView(view1, "SECTION_VIEW");

            var view2 = CreateSection(sectionViewFamilyType.Id,
                new XYZ(X2, center.Y, center.Z),
                new XYZ(0, 1, 0),
                new XYZ(0, 0, 1),
                new XYZ(-1, 0, 0),
                depth + 2 * secBuffer,
                H + 2 * secBuffer,
              1,
                GetUniqueViewName($"SECTION 2 ({nameLevel})")
            );
            SetTemplateForView(view2, "SECTION_VIEW");

            var view3 = CreateSection(sectionViewFamilyType.Id,
                new XYZ(center.X, Y1, center.Z),
                new XYZ(-1, 0, 0),
                new XYZ(0, 0, 1),
                new XYZ(0, 1, 0),
                width + 2 * secBuffer,
                H + 2 * secBuffer,
               1,
                GetUniqueViewName($"SECTION 3 ({nameLevel})")
            );
            SetTemplateForView(view3, "SECTION_VIEW");

            var view4 = CreateSection(sectionViewFamilyType.Id,
                new XYZ(center.X, Y2, center.Z),
                new XYZ(-1, 0, 0),
                new XYZ(0, 0, 1),
                new XYZ(0, -1, 0),
                width + 2 * secBuffer,
                H + 2 * secBuffer,
              1,
                GetUniqueViewName($"SECTION 4 ({nameLevel})")
            );
            SetTemplateForView(view4, "SECTION_VIEW");

            listView.Add(viewNorth);
            listView.Add(viewWest);
            listView.Add(viewEast);
            listView.Add(viewSouth);
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewNorth, "SHEET", "02. ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewWest, "SHEET", "02. ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewEast, "SHEET", "02. ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewSouth, "SHEET", "02. ELEVATION VIEW");

            listView.Add(view1);
            listView.Add(view2);
            listView.Add(view3);
            listView.Add(view4);
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view1, "SHEET", "03. SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view2, "SHEET", "03. SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view3, "SHEET", "03. SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view4, "SHEET", "03. SECTION VIEW");
            #endregion

            #region tạo section và elevation view cho thép
            // Hướng nhìn từ Tây sang Đông (Marker đặt ở phía Tây nhìn về phía Đông - Index 0)
            XYZ posWestRebar = new XYZ(max.X + offset, center.Y, center.Z);
            ElevationMarker markerWestRebar = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posWestRebar, 50);
            ViewSection viewWestRebar = markerWestRebar.CreateElevation(RevitClass.Doc, viewPlan.Id, 0);
            SetupView(viewWestRebar, combinedBBox, GetUniqueViewName($"D View Bar ({nameLevel})"));
            SetTemplateForView(viewWestRebar, "ELEVATION_V_PPVC_REBAR");

            // Hướng nhìn từ Nam lên Bắc (Marker đặt ở phía Nam nhìn lên phía Bắc - Index 1)
            XYZ posSouthRebar = new XYZ(center.X, min.Y - offset, center.Z);
            ElevationMarker markerSouthRebar = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posSouthRebar, 50);
            ViewSection viewSouthRebar = markerSouthRebar.CreateElevation(RevitClass.Doc, viewPlan.Id, 1);
            SetupView(viewSouthRebar, combinedBBox, GetUniqueViewName($"A View Bar ({nameLevel})"));
            SetTemplateForView(viewSouthRebar, "ELEVATION_H_PPVC_REBAR");

            // Hướng nhìn từ Đông sang Tây (Marker đặt ở phía Đông nhìn về phía Tây - Index 2)
            XYZ posEastRebar = new XYZ(min.X - offset, center.Y, center.Z);
            ElevationMarker markerEastRebar = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posEastRebar, 50);
            ViewSection viewEastRebar = markerEastRebar.CreateElevation(RevitClass.Doc, viewPlan.Id, 2);
            SetupView(viewEastRebar, combinedBBox, GetUniqueViewName($"B View Bar ({nameLevel})"));
            SetTemplateForView(viewEastRebar, "ELEVATION_V_PPVC_REBAR");

            // Hướng nhìn từ Bắc xuống Nam (Marker đặt ở phía Bắc nhìn xuống phía Nam - Index 3)
            XYZ posNorthRebar = new XYZ(center.X, max.Y + offset, center.Z);
            ElevationMarker markerNorthRebar = ElevationMarker.CreateElevationMarker(RevitClass.Doc, elevationViewFamilyType.Id, posNorthRebar, 50);
            ViewSection viewNorthRebar = markerNorthRebar.CreateElevation(RevitClass.Doc, viewPlan.Id, 3);
            SetupView(viewNorthRebar, combinedBBox, GetUniqueViewName($"C View Bar ({nameLevel})"));
            SetTemplateForView(viewNorthRebar, "ELEVATION_H_PPVC_REBAR");

            // --- TẠO 4 MẶT CẮT THEO YÊU CẦU ---
            double X1Rebar = min.X + 0.25 * width;
            double X2Rebar = min.X + 0.75 * width;
            double Y1Rebar = min.Y + 0.25 * depth;
            double Y2Rebar = min.Y + 0.75 * depth;
            double HRebar = max.Z - min.Z;

            double secBufferRebar = 0.5; // feet

            var view1Rebar = CreateSection(sectionViewFamilyType.Id,
                new XYZ(X1Rebar, center.Y, center.Z),
                new XYZ(0, 1, 0),
                new XYZ(0, 0, 1),
                new XYZ(1, 0, 0),
                depth + 2 * secBufferRebar,
                HRebar + 2 * secBufferRebar,
               1,
                GetUniqueViewName($"BAR SECTION 1 ({nameLevel})")
            );
            SetTemplateForView(view1Rebar, "SECTION_PPVC_REBAR");

            var view2Rebar = CreateSection(sectionViewFamilyType.Id,
                new XYZ(X2Rebar, center.Y, center.Z),
                new XYZ(0, 1, 0),
                new XYZ(0, 0, 1),
                new XYZ(-1, 0, 0),
                depth + 2 * secBufferRebar,
                HRebar + 2 * secBufferRebar,
              1,
                GetUniqueViewName($"BAR SECTION 2 ({nameLevel})")
            );
            SetTemplateForView(view2Rebar, "SECTION_PPVC_REBAR");

            var view3Rebar = CreateSection(sectionViewFamilyType.Id,
                new XYZ(center.X, Y1Rebar, center.Z),
                new XYZ(-1, 0, 0),
                new XYZ(0, 0, 1),
                new XYZ(0, 1, 0),
                width + 2 * secBufferRebar,
                HRebar + 2 * secBufferRebar,
               1,
                GetUniqueViewName($"BAR SECTION 3 ({nameLevel})")
            );
            SetTemplateForView(view3Rebar, "SECTION_PPVC_REBAR");

            var view4Rebar = CreateSection(sectionViewFamilyType.Id,
                new XYZ(center.X, Y2Rebar, center.Z),
                new XYZ(-1, 0, 0),
                new XYZ(0, 0, 1),
                new XYZ(0, -1, 0),
                width + 2 * secBufferRebar,
                HRebar + 2 * secBufferRebar,
              1,
                GetUniqueViewName($"BAR SECTION 4 ({nameLevel})")
            );
            SetTemplateForView(view4Rebar, "SECTION_PPVC_REBAR");

            listView.Add(viewNorthRebar);
            listView.Add(viewWestRebar);
            listView.Add(viewEastRebar);
            listView.Add(viewSouthRebar);
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewNorthRebar, "SHEET", "06. BAR ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewWestRebar, "SHEET", "06. BAR ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewEastRebar, "SHEET", "06. BAR ELEVATION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(viewSouthRebar, "SHEET", "06. BAR ELEVATION VIEW");

            listView.Add(view1Rebar);
            listView.Add(view2Rebar);
            listView.Add(view3Rebar);
            listView.Add(view4Rebar);
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view1Rebar, "SHEET", "07. BAR SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view2Rebar, "SHEET", "07. BAR SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view3Rebar, "SHEET", "07. BAR SECTION VIEW");
            PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(view4Rebar, "SHEET", "07. BAR SECTION VIEW");


            #endregion
        }

        /// <summary>
        /// Tạo các view Plan cho cả phần Bê tông (Casscast) và phần Thép (Rebar).
        /// </summary>
        private static void CreatePlanViews(
            ViewPlan viewPlan,
            BoundingBoxXYZ combinedBBox,
            string nameLevel,
            string nameProject,
            string namePPVC)
        {
            // Lấy ViewFamilyType phù hợp cho FloorPlan hoặc StructuralPlan
            ViewFamilyType planViewFamilyType = new FilteredElementCollector(RevitClass.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.FloorPlan)
                ?? new FilteredElementCollector(RevitClass.Doc)
                .OfClass(typeof(ViewFamilyType))
                .Cast<ViewFamilyType>()
                .FirstOrDefault(x => x.ViewFamily == ViewFamily.StructuralPlan);

            Level planLevel = null;
            if (viewPlan != null)
            {
                planLevel = viewPlan.GenLevel;
            }
            if (planLevel == null)
            {
                planLevel = new FilteredElementCollector(RevitClass.Doc)
                    .OfClass(typeof(Level))
                    .Cast<Level>()
                    .FirstOrDefault();
            }


            #region casscasst
            ViewPlan baseSlabView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                baseSlabView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(baseSlabView, combinedBBox, GetUniqueViewName($"{namePPVC} BASE SLAB LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabView, "SHEET", "01. BASE VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabView, "PPVC", namePPVC);
                SetTemplateForView(baseSlabView, "SLAB_BASE_VIEW");
            }

            // Ví dụ tạo plan view thứ 2 cho phần Thép (Rebar)
            ViewPlan newMidWallView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                newMidWallView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(newMidWallView, combinedBBox, GetUniqueViewName($"{namePPVC} MID WALL LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Thép)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newMidWallView, "SHEET", "01. BASE VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newMidWallView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newMidWallView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newMidWallView, "PPVC", namePPVC);
                SetTemplateForView(newMidWallView, "MID_WALL_LAYOUT_VIEW");
            }

            ViewPlan newRoofSlabView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                newRoofSlabView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(newRoofSlabView, combinedBBox, GetUniqueViewName($"{namePPVC} ROOF SLAB LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Thép)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newRoofSlabView, "SHEET", "01. BASE VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newRoofSlabView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newRoofSlabView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(newRoofSlabView, "PPVC", namePPVC);
                SetTemplateForView(newRoofSlabView, "MID_WALL_LAYOUT_VIEW");
            }

            #endregion

            #region rebar 

            ViewPlan baseSlabBotBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                baseSlabBotBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(baseSlabBotBarView, combinedBBox, GetUniqueViewName($"{namePPVC} BASE SLAB BOTTOM BARS LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabBotBarView, "SHEET", "04. BASE&ROOF BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabBotBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabBotBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabBotBarView, "PPVC", namePPVC);
                SetTemplateForView(baseSlabBotBarView, "SLAB_BOT_BAR_VIEW");
            }

            ViewPlan baseSlabTopBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                baseSlabTopBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(baseSlabTopBarView, combinedBBox, GetUniqueViewName($"{namePPVC} BASE SLAB TOP BARS LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabTopBarView, "SHEET", "04. BASE&ROOF BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabTopBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabTopBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(baseSlabTopBarView, "PPVC", namePPVC);
                SetTemplateForView(baseSlabTopBarView, "SLAB_TOP_BAR_VIEW");
            }


            ViewPlan roofBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                roofBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(roofBarView, combinedBBox, GetUniqueViewName($"{namePPVC} ROOF BARS LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(roofBarView, "SHEET", "04. BASE&ROOF BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(roofBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(roofBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(roofBarView, "PPVC", namePPVC);
                SetTemplateForView(roofBarView, "ROOF_BAR_VIEW");
            }

            ViewPlan beamBotBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                beamBotBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(beamBotBarView, combinedBBox, GetUniqueViewName($"{namePPVC} BEAM - BOTTOM BARS LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamBotBarView, "SHEET", "05. BEAM&MID BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamBotBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamBotBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamBotBarView, "PPVC", namePPVC);
                SetTemplateForView(beamBotBarView, "BEAM_BOT_BAR_VIEW");
            }

            ViewPlan beamTopBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                beamTopBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(beamTopBarView, combinedBBox, GetUniqueViewName($"{namePPVC} BEAM - TOP BARS LAYOUT PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamTopBarView, "SHEET", "05. BEAM&MID BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamTopBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamTopBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(beamTopBarView, "PPVC", namePPVC);
                SetTemplateForView(beamTopBarView, "BEAM_TOP_BAR_VIEW");
            }

            ViewPlan midBarView = null;
            if (planViewFamilyType != null && planLevel != null)
            {
                midBarView = ViewPlan.Create(RevitClass.Doc, planViewFamilyType.Id, planLevel.Id);
                SetupView(midBarView, combinedBBox, GetUniqueViewName($"{namePPVC} MID LAYOUT BARS PLAN ({nameLevel})"));

                // Thiết lập các thông số cơ bản cho plan view mới (Bê tông)
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(midBarView, "SHEET", "05. BEAM&MID BAR VIEW");
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(midBarView, "Level", nameLevel);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(midBarView, "Project", nameProject);
                PPVCREVIT.Utils.ParameterUtils.SetParameterValueByName(midBarView, "PPVC", namePPVC);
                SetTemplateForView(midBarView, "MID_LAYOUT_BAR_VIEW");
            }

            #endregion
        }



        /// <summary>
        /// Thiết lập cấu hình crop box, chiều sâu hiển thị và kích hoạt chế độ crop cho view.
        /// </summary>
        private static void SetupView(View view, BoundingBoxXYZ combinedBBox, string name)
        {
            if (view == null) return;

            // Gán tên duy nhất
            view.Name = name;

            // Kích hoạt tính năng crop và hiển thị viền crop
            view.CropBoxActive = true;
            view.CropBoxVisible = false;

            // Lấy hệ trục tọa độ cục bộ của View (được xác định bởi CropBox.Transform)
            Transform transform = view.CropBox.Transform;
            Transform invTransform = transform.Inverse;

            // Lấy 8 đỉnh của BoundingBox tổng hợp
            XYZ min = combinedBBox.Min;
            XYZ max = combinedBBox.Max;

            XYZ[] corners = new XYZ[]
            {
                new XYZ(min.X, min.Y, min.Z),
                new XYZ(max.X, min.Y, min.Z),
                new XYZ(min.X, max.Y, min.Z),
                new XYZ(max.X, max.Y, min.Z),
                new XYZ(min.X, min.Y, max.Z),
                new XYZ(max.X, min.Y, max.Z),
                new XYZ(min.X, max.Y, max.Z),
                new XYZ(max.X, max.Y, max.Z)
            };

            // Chuyển đổi toàn bộ đỉnh về tọa độ cục bộ của View
            List<XYZ> localCorners = corners.Select(pt => invTransform.OfPoint(pt)).ToList();

            // Tính toán giới hạn Min và Max theo hệ tọa độ cục bộ của View
            double minX = localCorners.Min(pt => pt.X);
            double maxX = localCorners.Max(pt => pt.X);
            double minY = localCorners.Min(pt => pt.Y);
            double maxY = localCorners.Max(pt => pt.Y);
            double minZ = localCorners.Min(pt => pt.Z);
            double maxZ = localCorners.Max(pt => pt.Z);

            // Bổ sung lề đệm (buffer = 0.5 feet ~ 150mm)
            double buffer = 0.5;
            minX -= buffer;
            maxX += buffer;
            minY -= buffer;
            maxY += buffer;
            minZ -= buffer;
            maxZ += buffer;

            // Cập nhật CropBox
            BoundingBoxXYZ cropBox = view.CropBox;
            cropBox.Min = new XYZ(minX, minY, minZ);
            cropBox.Max = new XYZ(maxX, maxY, maxZ);
            view.CropBox = cropBox;

            // Điều chỉnh Far Clip Offset bằng thuộc tính hệ thống nếu là ViewSection
            if (view is ViewSection)
            {
                double depth = Math.Abs(minZ);
                Parameter farClipParam = view.get_Parameter(BuiltInParameter.VIEWER_BOUND_OFFSET_FAR);
                if (farClipParam != null && !farClipParam.IsReadOnly)
                {
                    farClipParam.Set(depth / 2.4);
                }
            }
        }

        private static void SetTemplateForView(View view, string templateName)
        {
            View template = GetViewTemplateByName(templateName);
            ElementId templateId = template != null ? template.Id : ElementId.InvalidElementId;
            view.ViewTemplateId = templateId;
        }

        private static View GetViewTemplateByName(string templateName)
        {
            return new FilteredElementCollector(RevitClass.Doc)
                .OfClass(typeof(View))
                .Cast<View>()
                .FirstOrDefault(v => v.IsTemplate && v.Name.Contains(templateName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Tạo một mặt cắt (Section View) với các thông số vị trí, hướng nhìn, kích thước và đặt tên.
        /// </summary>
        private static ViewSection CreateSection(ElementId sectionTypeId, XYZ origin, XYZ basisX, XYZ basisY, XYZ basisZ, double width, double height, double depth, string name)
        {
            Transform t = Transform.Identity;
            t.Origin = origin;
            t.BasisX = basisX;
            t.BasisY = basisY;
            t.BasisZ = basisZ;

            BoundingBoxXYZ sectionBox = new BoundingBoxXYZ();
            sectionBox.Transform = t;
            sectionBox.Min = new XYZ(-width / 2.0, -height / 2.0, -0.1);
            sectionBox.Max = new XYZ(width / 2.0, height / 2.0, depth);

            ViewSection sectionView = ViewSection.CreateSection(RevitClass.Doc, sectionTypeId, sectionBox);
            sectionView.Name = name;
            sectionView.CropBoxActive = true;
            sectionView.CropBoxVisible = false;

            return sectionView;
        }

        /// <summary>
        /// Tạo tên view không bị trùng lặp bằng cách đánh số thứ tự nếu trùng tên.
        /// </summary>
        private static string GetUniqueViewName(string baseName)
        {
            string name = baseName;
            int counter = 1;
            while (true)
            {
                bool exists = new FilteredElementCollector(RevitClass.Doc)
                    .OfClass(typeof(View))
                    .Cast<View>()
                    .Any(v => v.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

                if (!exists)
                    return name;

                name = $"{baseName}_{counter}";
                counter++;
            }
        }
    }
}

