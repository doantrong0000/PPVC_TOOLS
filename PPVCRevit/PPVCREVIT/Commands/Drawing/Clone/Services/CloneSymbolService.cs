using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Services
{
    public class CloneSymbolService
    {
        private Document _doc;

        public CloneSymbolService(Document doc)
        {
            _doc = doc;
        }

        public int CloneSymbols(View sourceView, View targetView, Transform transform)
        {
            var symbols = new FilteredElementCollector(_doc, sourceView.Id)
                .OfClass(typeof(FamilyInstance))
                .OfCategory(BuiltInCategory.OST_GenericAnnotation)
                .Cast<FamilyInstance>()
                .Select(t => t.Id)
                .ToList();

            if (symbols.Count == 0) return 0;

            // Copy symbols
            CopyPasteOptions options = new CopyPasteOptions();
            var copiedIds = ElementTransformUtils.CopyElements(sourceView, symbols, targetView, transform, options);

            return copiedIds.Count;
        }
    }
}
