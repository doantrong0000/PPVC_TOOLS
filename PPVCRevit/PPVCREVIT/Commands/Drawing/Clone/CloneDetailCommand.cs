using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using PPVCREVIT.Commands.Drawing.Clone.Helpers;
using PPVCREVIT.Commands.Drawing.Clone.Services;
using PPVCREVIT.Commands.Drawing.Clone.Views;
using System;

namespace PPVCREVIT.Commands.Drawing.Clone
{
    [Transaction(TransactionMode.Manual)]
    public class CloneDetailCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiapp = commandData.Application;
            UIDocument uidoc = uiapp.ActiveUIDocument;
            Document doc = uidoc.Document;
            View activeView = uidoc.ActiveView;

#if !DEBUG
            try
            {
#endif
                if (activeView == null || activeView.ViewType == ViewType.ThreeD || activeView.ViewType == ViewType.Schedule || activeView.ViewType == ViewType.DrawingSheet)
                {
                    TaskDialog.Show("Lỗi", "Lệnh này chỉ chạy được trên View mặt bằng, mặt cắt, hoặc drafting view.");
                    return Result.Failed;
                }

                CloneDetailWindow window = new CloneDetailWindow(doc, activeView);
                bool? result = window.ShowDialog();

                if (result != true || window.SelectedSourceView == null)
                {
                    return Result.Cancelled;
                }

                View sourceView = window.SelectedSourceView;

                CloneTagService tagService = new CloneTagService(doc);
                CloneDimensionService dimService = new CloneDimensionService(doc);
                CloneTextService textService = new CloneTextService(doc);
                CloneSymbolService symbolService = new CloneSymbolService(doc);

                int clonedTags = 0;
                int clonedDims = 0;
                int clonedTexts = 0;
                int clonedSymbols = 0;

                using (Transaction tx = new Transaction(doc, "Semantic Clone 2D Details"))
                {
                    tx.Start();

                    // 1. Calculate Global Transform using Anchor Pair
                    Transform finalTransform = Transform.Identity;
                    var sourceElems = new FilteredElementCollector(doc, sourceView.Id)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        .ToList();

                    var targetElems = new FilteredElementCollector(doc, activeView.Id)
                        .WhereElementIsNotElementType()
                        .Where(e => e.Category != null && e.Category.CategoryType == CategoryType.Model)
                        .ToList();

                    // Find anchor source: prioritize FamilyInstance for true Transform, pick the largest one
                    Element anchorSource = sourceElems.OfType<FamilyInstance>()
                        .OrderByDescending(e => new ElementSignature(e).Volume)
                        .FirstOrDefault();

                    if (anchorSource == null)
                        anchorSource = sourceElems.FirstOrDefault();

                    if (anchorSource != null)
                    {
                        ElementSignature anchorSig = new ElementSignature(anchorSource);
                        Element anchorTarget = targetElems.FirstOrDefault(e => anchorSig.IsIdenticalTo(new ElementSignature(e)));

                        if (anchorTarget != null)
                        {
                            Transform sourceTrans = Transform.Identity;
                            if (anchorSource is FamilyInstance sInst) sourceTrans = sInst.GetTransform();
                            else if (anchorSource.get_BoundingBox(null) != null) sourceTrans = Transform.CreateTranslation(anchorSource.get_BoundingBox(null).Min);

                            Transform targetTrans = Transform.Identity;
                            if (anchorTarget is FamilyInstance tInst) targetTrans = tInst.GetTransform();
                            else if (anchorTarget.get_BoundingBox(null) != null) targetTrans = Transform.CreateTranslation(anchorTarget.get_BoundingBox(null).Min);

                            finalTransform = targetTrans.Multiply(sourceTrans.Inverse);
                        }
                    }

                    // 2. Clone Texts
                    clonedTexts = textService.CloneTexts(sourceView, activeView, finalTransform);

                    // 3. Clone Tags
                    clonedTags = tagService.CloneTags(sourceView, activeView, finalTransform);
                    
                    // 4. Clone Symbols
                    clonedSymbols = symbolService.CloneSymbols(sourceView, activeView, finalTransform);

                    // 5. Clone Dimensions (Temporarily disabled)
                    // clonedDims = dimService.CloneDimensions(sourceView, activeView);

                    tx.Commit();
                }
                window.Close();

                TaskDialog.Show("Clone Complete", $"Successfully cloned:\n- {clonedTexts} TextNotes\n- {clonedTags} Tags\n- {clonedSymbols} Symbols\n- {clonedDims} Dimensions");
                return Result.Succeeded;
#if !DEBUG
            }
            catch (Exception ex)
            {
                TaskDialog.Show("Error", ex.Message);
                return Result.Failed;
            }
#endif
        }
    }
}
