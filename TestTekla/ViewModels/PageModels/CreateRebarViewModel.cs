using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Geometry3d;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;

namespace TeklaApp.ViewModels.PageModels
{
    public class CreateRebarViewModel
    {
        private Model _model = new Model();

        public string CreateRebarWithMultiPoints(double targetSpace, double startCover, double endCover, out int zoneCount)
        {
            zoneCount = 0;

            if (!_model.GetConnectionStatus())
            {
                return "Error: Tekla not connected!";
            }

            if (targetSpace <= 0)
            {
                return "Error: Target spacing must be greater than 0.";
            }

            try
            {
                Picker picker = new Picker();

                // 1. Pick Part
                Part pickedPart = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Pick a part to reinforce") as Part;
                if (pickedPart == null) return "Cancelled: No part picked.";

                // 2. Pick Shape
                ArrayList shapePointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Pick shape points (Middle mouse button to finish)");
                if (shapePointsList.Count < 2)
                {
                    return "Cancelled: Need at least 2 shape points.";
                }

                // 3. Pick Distribution
                ArrayList distPointsList = picker.PickPoints(Picker.PickPointEnum.PICK_POLYGON, "Pick multiple distribution points for segments: P1-P2 (Gap) P3-P4 ... (Middle mouse)");
                if (distPointsList.Count < 2)
                {
                    return "Cancelled: Need at least 2 distribution points.";
                }

                List<Tekla.Structures.Geometry3d.Point> distPoints = new List<Tekla.Structures.Geometry3d.Point>();
                foreach (Tekla.Structures.Geometry3d.Point pt in distPointsList) distPoints.Add(pt);

                // If odd number, omit the last
                if (distPoints.Count % 2 != 0)
                {
                    distPoints.RemoveAt(distPoints.Count - 1);
                }

                if (distPoints.Count < 2)
                {
                    return "Cancelled: Need at least 2 valid distribution points.";
                }

                Polygon shapePolygon = new Polygon();
                foreach (Tekla.Structures.Geometry3d.Point pt in shapePointsList)
                {
                    shapePolygon.Points.Add(pt);
                }

                List<ModelObject> createdGroups = new List<ModelObject>();
                ArrayList objectsToSelect = new ArrayList();

                ArrayList exactSpacings = new ArrayList();
                Tekla.Structures.Geometry3d.Point groupStart = null;
                Tekla.Structures.Geometry3d.Point groupEnd = null;

                // Apply From plane (cover) to the very first and very last point before calculating
                if (distPoints.Count >= 2)
                {
                    // Shift Start Point
                    if (startCover > 0)
                    {
                        var p0 = distPoints[0];
                        var p1 = distPoints[1];
                        Vector dirStart = new Vector(p1.X - p0.X, p1.Y - p0.Y, p1.Z - p0.Z);
                        dirStart.Normalize();
                        distPoints[0] = new Tekla.Structures.Geometry3d.Point(p0.X + dirStart.X * startCover, p0.Y + dirStart.Y * startCover, p0.Z + dirStart.Z * startCover);
                    }

                    // Shift End Point
                    if (endCover > 0)
                    {
                        int last = distPoints.Count - 1;
                        var pLast = distPoints[last];
                        var pPrev = distPoints[last - 1];
                        Vector dirEnd = new Vector(pPrev.X - pLast.X, pPrev.Y - pLast.Y, pPrev.Z - pLast.Z);
                        dirEnd.Normalize();
                        distPoints[last] = new Tekla.Structures.Geometry3d.Point(pLast.X + dirEnd.X * endCover, pLast.Y + dirEnd.Y * endCover, pLast.Z + dirEnd.Z * endCover);
                    }
                }

                for (int i = 0; i < distPoints.Count / 2; i++)
                {
                    Tekla.Structures.Geometry3d.Point segStart = distPoints[2 * i];
                    Tekla.Structures.Geometry3d.Point segEnd = distPoints[2 * i + 1];

                    double segLength = Distance.PointToPoint(segStart, segEnd);
                    if (segLength < 1.0) continue; // skip tiny segments

                    if (groupStart == null)
                    {
                        groupStart = segStart;
                    }
                    else
                    {
                        // Calculate gap from previous segment's end to current segment's start
                        Tekla.Structures.Geometry3d.Point prevEnd = distPoints[2 * (i - 1) + 1];
                        double gap = Distance.PointToPoint(prevEnd, segStart);
                        exactSpacings.Add(gap);
                    }

                    // Calculate equal spaces for this specific segment
                    int numSpaces = (int)Math.Round(segLength / targetSpace);
                    if (numSpaces < 1) numSpaces = 1;

                    double exactSpace = segLength / numSpaces;
                    for (int s = 0; s < numSpaces; s++)
                    {
                        exactSpacings.Add(exactSpace);
                    }

                    groupEnd = segEnd;
                }

                if (groupStart != null && groupEnd != null)
                {
                    RebarGroup rg = new RebarGroup();
                    rg.Father = pickedPart;
                    rg.Polygons.Add(shapePolygon);

                    // Assign exact start & end of the combined group
                    rg.StartPoint = groupStart;
                    rg.EndPoint = groupEnd;

                    // Provide dummy/default values so Tekla doesn't complain about missing information. 
                    // The user will manually adjust them later in the model.
                    rg.Name = "REBAR";
                    rg.Size = "10";
                    rg.Grade = "H";
                    rg.Class = 2;
                    rg.RadiusValues.Add(20.0); // MUST be double

                    // Use the exactly calculated spacings list
                    rg.SpacingType = BaseRebarGroup.RebarGroupSpacingTypeEnum.SPACING_TYPE_EXACT_SPACINGS;
                    rg.Spacings = exactSpacings;

                    if (rg.Insert())
                    {
                        createdGroups.Add(rg);
                        objectsToSelect.Add(rg);
                    }
                }

                _model.CommitChanges();

                // Select the created groups in Tekla UI
                if (objectsToSelect.Count > 0)
                {
                    Tekla.Structures.Model.UI.ModelObjectSelector selector = new Tekla.Structures.Model.UI.ModelObjectSelector();
                    selector.Select(objectsToSelect);
                }

                zoneCount = createdGroups.Count;
                if (zoneCount > 0)
                {
                    return $"Successfully created {zoneCount} unified Rebar Group containing all segments.";
                }
                else
                {
                    return "Error: Failed to create any RebarGroup.";
                }

            }
            catch (Exception ex)
            {
                if (ex.GetType().Name.Contains("Picker") || ex.Message.Contains("interrupt"))
                    return "Cancelled by user.";
                return "Error: " + ex.Message;
            }
        }
    }
}
