using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using PPVCREVIT.Commands.Drawing.CreateFloorStep.Model;
using System;
using System.Collections.Generic;
using System.Linq;

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

                // Loại bỏ trùng lặp nếu người dùng quét trúng 1 cấu kiện nhiều lần
                allFloorData = allFloorData.GroupBy(x => x.FloorElement.UniqueId).Select(g => g.First()).ToList();

                if (allFloorData.Count < 2)
                {
                    TaskDialog.Show("Thông báo", "Vui lòng chọn ít nhất 2 sàn để tạo giật cấp.");
                    return Result.Cancelled;
                }

                // Sử dụng chung logic tạo ở Model
                CreateFloorStepModel.CreateStepBetweenFloors(doc, uidoc, allFloorData);
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Lỗi Hệ Thống", ex.ToString());
            }

            return Result.Succeeded;
        }
    }

    public class LinkFloorSelectionFilter : ISelectionFilter
    {
        private Document _hostDoc;
        public LinkFloorSelectionFilter(Document hostDoc)
        {
            _hostDoc = hostDoc;
        }

        public bool AllowElement(Element elem)
        {
            return elem is RevitLinkInstance;
        }

        public bool AllowReference(Reference reference, XYZ position)
        {
            if (reference.LinkedElementId != ElementId.InvalidElementId)
            {
                RevitLinkInstance linkInst = _hostDoc.GetElement(reference.ElementId) as RevitLinkInstance;
                if (linkInst != null)
                {
                    Document linkDoc = linkInst.GetLinkDocument();
                    if (linkDoc != null)
                    {
                        Element linkedElem = linkDoc.GetElement(reference.LinkedElementId);
                        return linkedElem is Floor;
                    }
                }
            }
            return true;
        }
    }
}