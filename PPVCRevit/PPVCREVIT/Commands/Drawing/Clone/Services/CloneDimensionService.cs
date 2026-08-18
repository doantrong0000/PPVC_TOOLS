using Autodesk.Revit.DB;
using PPVCREVIT.Commands.Drawing.Clone.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Services
{
    public class CloneDimensionService
    {
        private Document _doc;

        public CloneDimensionService(Document doc)
        {
            _doc = doc;
        }

        private XYZ GetElementReferencePoint(Element elem)
        {
            BoundingBoxXYZ bbox = elem.get_BoundingBox(null);
            if (bbox != null)
            {
                return bbox.Min;
            }
            return XYZ.Zero;
        }

        public int CloneDimensions(View sourceView, View targetView)
        {
            int clonedCount = 0;

            var sourceDims = new FilteredElementCollector(_doc, sourceView.Id)
                .OfClass(typeof(Dimension))
                .Cast<Dimension>()
                .Where(d => d.References.Size > 0)
                .ToList();

            foreach (var dim in sourceDims)
            {
#if !DEBUG
                try
                {
#endif
                    ReferenceArray newRefs = new ReferenceArray();
                    HashSet<string> addedRefStrings = new HashSet<string>();
                    bool allRefsFound = true;

                    // Try to get Dimension Line
                    Line dimLine = dim.Curve as Line;
                    if (dimLine == null) continue;

                    // Compute translation vector for the dimension line.
                    XYZ translationVector = XYZ.Zero;
                    bool translationComputed = false;

                    foreach (Reference r in dim.References)
                    {
                        Element sourceElem = _doc.GetElement(r);
                        if (sourceElem == null)
                        {
                            allRefsFound = false;
                            break;
                        }

                        Element targetElem = GeometryMatchHelper.FindMatchingElement(_doc, sourceElem, targetView);
                        if (targetElem == null)
                        {
                            allRefsFound = false;
                            break;
                        }

                        if (!translationComputed && sourceElem.Id != targetElem.Id)
                        {
                            XYZ sourcePt = GetElementReferencePoint(sourceElem);
                            XYZ targetPt = GetElementReferencePoint(targetElem);
                            translationVector = targetPt - sourcePt;
                            translationComputed = true;
                        }

                        // Map the reference using Stable Representation hack
                        string sourceStableRef = r.ConvertToStableRepresentation(_doc);
                        string targetStableRef = sourceStableRef.Replace(sourceElem.UniqueId, targetElem.UniqueId);

                        Reference targetRef = null;
#if !DEBUG
                        try
                        {
#endif
                            targetRef = Reference.ParseFromStableRepresentation(_doc, targetStableRef);
#if !DEBUG
                        }
                        catch
                        {
                            // If parsing fails, we skip this dimension
                            targetRef = null;
                        }
#endif

                        if (targetRef != null)
                        {
                            string stableRef = targetRef.ConvertToStableRepresentation(_doc);
                            if (!addedRefStrings.Contains(stableRef))
                            {
                                newRefs.Append(targetRef);
                                addedRefStrings.Add(stableRef);
                            }
                        }
                        else
                        {
                            allRefsFound = false;
                            break;
                        }
                    }

                    if (allRefsFound && newRefs.Size > 1)
                    {
                        Line newLine = Line.CreateUnbound(dimLine.Origin + translationVector, dimLine.Direction);
                        Dimension newDim = _doc.Create.NewDimension(targetView, newLine, newRefs);
                        newDim.ChangeTypeId(dim.GetTypeId());
                        clonedCount++;
                    }
#if !DEBUG
                }
                catch
                {
                    // Ignore dimension if it fails to clone due to geometry mismatches
                    continue;
                }
#endif
            }

            return clonedCount;
        }
    }
}
