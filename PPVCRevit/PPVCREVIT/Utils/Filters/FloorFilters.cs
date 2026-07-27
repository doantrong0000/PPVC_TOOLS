using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Utils.Filters
{
    public static class FloorFilters
    {
        public class LocalFloorSelectionFilter : ISelectionFilter
        {
            public bool AllowElement(Element elem)
            {
                return elem is Floor;
            }

            public bool AllowReference(Reference reference, XYZ position)
            {
                return true;
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
}
