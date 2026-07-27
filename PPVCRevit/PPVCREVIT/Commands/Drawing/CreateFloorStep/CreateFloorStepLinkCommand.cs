using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Commands.Drawing.CreateFloorStep.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using static PPVCREVIT.Utils.Filters.FloorFilters;

namespace PPVCREVIT.Commands.Drawing
{
    [Transaction(TransactionMode.Manual)]
    public class CreateFloorStepLinkCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                List<FloorData> allFloorData = new List<FloorData>();

                // Chọn sàn trong link có chuyển đổi
                try
                {
                    IList<Reference> linkRefs = uidoc.Selection.PickObjects(ObjectType.LinkedElement, new LinkFloorSelectionFilter(doc), "Quét chọn các Sàn trong file Link");
                    foreach (Reference r in linkRefs)
                    {
                        if (r.LinkedElementId != ElementId.InvalidElementId)
                        {
                            RevitLinkInstance linkInst = doc.GetElement(r.ElementId) as RevitLinkInstance;
                            if (linkInst != null)
                            {
                                Document linkDoc = linkInst.GetLinkDocument();
                                if (linkDoc != null)
                                {
                                    Floor linkedFloor = linkDoc.GetElement(r.LinkedElementId) as Floor;
                                    if (linkedFloor != null)
                                    {
                                        allFloorData.Add(new FloorData
                                        {
                                            FloorElement = linkedFloor,
                                            LinkTransform = linkInst.GetTotalTransform(),
                                            SourceName = linkDoc.Title
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Autodesk.Revit.Exceptions.OperationCanceledException)
                {
                    return Result.Cancelled;
                }

                if (allFloorData.Count < 1)

                {
                    return Result.Cancelled;
                }


                // Loại bỏ trùng lặp nếu người dùng quét trúng 1 cấu kiện nhiều lần
                allFloorData = allFloorData.GroupBy(x => x.FloorElement.UniqueId).Select(g => g.First()).ToList();


                if (allFloorData.Count >= 2)
                {
                    CreateFloorStepModel.CreateStepBetweenFloors(doc, uidoc, allFloorData);
                }


                foreach (var floorData in allFloorData)
                {
                    CreateFloorStepModel.CreateInternalStepByOverlappingFaces(doc, uidoc, floorData);
                }
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Hệ Thống", ex.ToString());
            }

            return Result.Succeeded;
        }
    }


}