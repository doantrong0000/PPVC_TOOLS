using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using PPVCREVIT.Commands.Drawing.Clone.Helpers;
using PPVCREVIT.Utils.Tag;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Services
{
    public class CloneTagService
    {
        private Document _doc;

        public CloneTagService(Document doc)
        {
            _doc = doc;
        }

        public int CloneTags(View sourceView, View targetView, Transform transform)
        {
            int clonedCount = 0;

            // Get all IndependentTags in source view
            var sourceTags = new FilteredElementCollector(_doc, sourceView.Id)
                .OfClass(typeof(IndependentTag))
                .Cast<IndependentTag>()
                .ToList();

            // Get all Rebars in target view to match against
            var targetRebars = new FilteredElementCollector(_doc, targetView.Id)
                .OfClass(typeof(Rebar))
                .Cast<Rebar>()
                .ToList();

            foreach (var tag in sourceTags)
            {
                // Get tagged element (Compatible with Revit 2022+)
                var hostIds = tag.GetTaggedLocalElementIds();
                if (hostIds == null || hostIds.Count == 0) continue;

                ElementId hostId = hostIds.First();
                if (hostId == ElementId.InvalidElementId) continue;

                Element sourceHost = _doc.GetElement(hostId);
                Element targetHost = null;

                // Match host
                if (sourceHost is Rebar sourceRebar)
                {
                    targetHost = RebarMatchHelper.FindMatch(sourceRebar, targetRebars);
                }
                else
                {
                    targetHost = GeometryMatchHelper.FindMatchingElement(_doc, sourceHost, targetView);
                }

                if (targetHost != null)
                {
                    // Calculate new tag position using Global Transform
                    XYZ newTagHeadPosition = transform.OfPoint(tag.TagHeadPosition);

                    Reference hostRef = null;

                    if (targetHost is Rebar targetRebar)
                    {
                        // 1. CỰC KỲ QUAN TRỌNG: Ép thanh thép phải hiển thị (không bị bê tông che) trên View đích
                        if (!targetRebar.IsUnobscuredInView(targetView))
                        {
                            targetRebar.SetUnobscuredInView(targetView, true);
                        }

                        // 2. Bắt buộc Regenerate để Revit "vẽ" lại hình học của thanh thép ra View
                        _doc.Regenerate();

                        // 3. Lấy Reference từ hình học (Curve/Face)
                        hostRef = RebarTagUltis.GetRebarReference(targetRebar, targetView);

                        // 4. Bẫy lỗi an toàn: Nếu vẫn null (do thép nằm ngoài View Crop Region)
                        if (hostRef == null)
                        {
                            throw new Exception($"Không thể lấy được hình học của thanh thép ID {targetRebar.Id} trên View. Hãy kiểm tra xem thép có nằm trong vùng nhìn (Crop Region) không.");
                        }
                    }
                    else
                    {
                        // Với dầm, cột, tường... thì dùng cái này bình thường
                        hostRef = new Reference(targetHost);
                    }

                    // 5. Tạo Tag
                    IndependentTag newTag = IndependentTag.Create(
                        _doc,
                        tag.GetTypeId(),
                        targetView.Id,
                        hostRef,
                        tag.HasLeader,
                        tag.TagOrientation,
                        newTagHeadPosition);

                    if (tag.HasLeader)
                    {
                        newTag.LeaderEndCondition = tag.LeaderEndCondition;

                        // Only manually set the Leader End if it's a Free end. 
                        // If it's Attached, Revit calculates it automatically.
                        if (tag.LeaderEndCondition == LeaderEndCondition.Free)
                        {
#if !DEBUG
                            try
                            {
#endif
                                Reference sourceRef = (sourceHost is Rebar sourceR) 
                                    ? RebarTagUltis.GetRebarReference(sourceR, sourceView) 
                                    : new Reference(sourceHost);
                                    
                                XYZ oldLeaderEnd = tag.GetLeaderEnd(sourceRef);
                                newTag.SetLeaderEnd(hostRef, transform.OfPoint(oldLeaderEnd));
#if !DEBUG
                            }
                            catch { } // Ignore if we can't set leader end properly
#endif
                        }
                    }

                    clonedCount++;
                }
            }

            return clonedCount;
        }
    }
}
