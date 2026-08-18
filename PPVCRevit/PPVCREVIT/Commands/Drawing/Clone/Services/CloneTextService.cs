using Autodesk.Revit.DB;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Services
{
    public class CloneTextService
    {
        private Document _doc;

        public CloneTextService(Document doc)
        {
            _doc = doc;
        }

        public int CloneTexts(View sourceView, View targetView, Transform transform)
        {
            var textNotes = new FilteredElementCollector(_doc, sourceView.Id)
                .OfClass(typeof(TextNote))
                .Cast<TextNote>()
                .Select(t => t.Id)
                .ToList();

            if (textNotes.Count == 0) return 0;

            // Copy text notes
            CopyPasteOptions options = new CopyPasteOptions();
            
            var copiedIds = ElementTransformUtils.CopyElements(sourceView, textNotes, targetView, transform, options);

            return copiedIds.Count;
        }
    }
}
