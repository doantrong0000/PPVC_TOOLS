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
    public class CreateFloorStepCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                List<FloorData> allFloorData = new List<FloorData>();

                try
                {
                    IList<Reference> localRefs = uidoc.Selection.PickObjects(ObjectType.Element, new LocalFloorSelectionFilter(), "Quét chọn các Sàn trong project hiện tại");
                    foreach (Reference r in localRefs)
                    {
                        Floor localFloor = doc.GetElement(r) as Floor;
                        if (localFloor != null)
                        {
                            allFloorData.Add(new FloorData
                            {
                                FloorElement = localFloor,
                                LinkTransform = Transform.Identity,
                                SourceName = "Host_File"
                            });
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