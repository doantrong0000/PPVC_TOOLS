using System;
using Tekla.Structures.Model;
using Tekla.Structures.Model.UI;
using Tekla.Structures.Drawing;
using System.Windows;

namespace TeklaApp.ViewModels.PageModels
{
    public class PPVCAutoDimTagViewModel
    {
        public void CreateCastUnitDrawing(string settingName, out string status)
        {
            status = "";
            Model model = new Model();
            if (!model.GetConnectionStatus())
            {
                status = "Not connected to Tekla Structures.";
                return;
            }

            try
            {
                Picker picker = new Picker();
                Tekla.Structures.Model.ModelObject pickedObj = picker.PickObject(Picker.PickObjectEnum.PICK_ONE_PART, "Select Main Part to create Cast Unit Drawing");
                if (pickedObj is Tekla.Structures.Model.Part part)
                {
                    Assembly castUnit = part.GetAssembly();
                    if (castUnit != null)
                    {
                        Tekla.Structures.Drawing.CastUnitDrawing cuDrawing = new Tekla.Structures.Drawing.CastUnitDrawing(castUnit.Identifier, settingName);
                        if (cuDrawing.Insert())
                        {
                            status = "Cast Unit drawing created successfully for: " + part.Name;
                        }
                        else
                        {
                            status = "Failed to insert Cast Unit drawing. Make sure drawing numbering is up to date.";
                        }
                    }
                    else
                    {
                        status = "Failed to get Cast Unit from selected part.";
                    }
                }
            }
            catch (Exception ex)
            {
                status = "Error: " + ex.Message;
            }
        }
        public void CreateBasicSections(out string status)
        {
            status = "";
            var dh = new DrawingHandler();
            var drawing = dh.GetActiveDrawing();
            if (drawing == null)
            {
                status = "No active drawing open.";
                return;
            }

            try
            {
                var selector = dh.GetDrawingObjectSelector();
                var selectedEnum = selector.GetSelected();

                Tekla.Structures.Drawing.View elevationView = null;
                while (selectedEnum.MoveNext())
                {
                    if (selectedEnum.Current is Tekla.Structures.Drawing.View v)
                    {
                        elevationView = v;
                        break;
                    }
                }

                if (elevationView == null)
                {
                    status = "Please select an elevation view in the drawing first.";
                    return;
                }

                double minY = double.MaxValue;
                double maxY = double.MinValue;
                double minX = double.MaxValue;
                double maxX = double.MinValue;
                bool found = false;

                Tekla.Structures.Model.Model model = new Tekla.Structures.Model.Model();
                Tekla.Structures.Geometry3d.Matrix toViewMatrix = Tekla.Structures.Geometry3d.MatrixFactory.ToCoordinateSystem(elevationView.DisplayCoordinateSystem);

                var partsEnum = elevationView.GetObjects(new Type[] { typeof(Tekla.Structures.Drawing.Part) });
                while (partsEnum.MoveNext())
                {
                    if (partsEnum.Current is Tekla.Structures.Drawing.Part dp)
                    {
                        var mPart = model.SelectModelObject(dp.ModelIdentifier) as Tekla.Structures.Model.Part;
                        if (mPart != null)
                        {
                            var solid = mPart.GetSolid();
                            if (solid != null)
                            {
                                var faceEnum = solid.GetFaceEnumerator();
                                while (faceEnum.MoveNext())
                                {
                                    if (faceEnum.Current is Tekla.Structures.Solid.Face face)
                                    {
                                        var loopEnum = face.GetLoopEnumerator();
                                        while (loopEnum.MoveNext())
                                        {
                                            if (loopEnum.Current is Tekla.Structures.Solid.Loop loop)
                                            {
                                                var vertEnum = loop.GetVertexEnumerator();
                                                while (vertEnum.MoveNext())
                                                {
                                                    if (vertEnum.Current is Tekla.Structures.Geometry3d.Point p)
                                                    {
                                                        var vp = toViewMatrix.Transform(p);
                                                        if (vp.X < minX) minX = vp.X;
                                                        if (vp.X > maxX) maxX = vp.X;
                                                        if (vp.Y < minY) minY = vp.Y;
                                                        if (vp.Y > maxY) maxY = vp.Y;
                                                        found = true;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                if (!found)
                {
                    minY = elevationView.RestrictionBox.MinPoint.Y;
                    maxY = elevationView.RestrictionBox.MaxPoint.Y;
                    minX = elevationView.RestrictionBox.MinPoint.X;
                    maxX = elevationView.RestrictionBox.MaxPoint.X;
                }

                double H = maxY - minY;
                if (H <= 0)
                {
                    status = "Invalid view height.";
                    return;
                }

                // Mái (Roof): 1.1 down to 0.85
                double yRoofCut = minY + 1.1 * H;
                double yRoofTarget = minY + 0.85 * H;
                CreateSingleSection(elevationView, minX, maxX, yRoofCut, yRoofTarget, "MAI", maxY + 2000 + H);

                // Tường (Wall): 0.6 down to 0.4
                double yWallCut = minY + 0.6 * H;
                double yWallTarget = minY + 0.4 * H;
                CreateSingleSection(elevationView, minX, maxX, yWallCut, yWallTarget, "TUONG", maxY + 2000);

                // Sàn (Floor): 0.3 down to -0.1
                double yFloorCut = minY + 0.3 * H;
                double yFloorTarget = minY - 0.1 * H;
                CreateSingleSection(elevationView, minX, maxX, yFloorCut, yFloorTarget, "SAN", maxY + 2000 - H);

                drawing.CommitChanges();
                status = "Created 3 sections successfully.";
            }
            catch (Exception ex)
            {
                status = "Error: " + ex.Message;
            }
        }

        private void CreateSingleSection(Tekla.Structures.Drawing.View elevationView, double minX, double maxX, double cutY, double targetY, string markName, double insertY)
        {
            // To look down, we set depthDown to the absolute difference
            double depthDown = Math.Abs(cutY - targetY);
            double depthUp = 200;

            var p1 = new Tekla.Structures.Geometry3d.Point(minX - 200, cutY, 0);
            var p2 = new Tekla.Structures.Geometry3d.Point(maxX + 200, cutY, 0);

            // Insertion point in global drawing coordinates. We place them to the right.
            var insertionPoint = new Tekla.Structures.Geometry3d.Point(maxX + 3000, insertY, 0);

            var viewAttr = new Tekla.Structures.Drawing.View.ViewAttributes();
            viewAttr.LoadAttributes("standard");

            var markAttr = new Tekla.Structures.Drawing.SectionMarkBase.SectionMarkAttributes();
            markAttr.LoadAttributes("standard");
            markAttr.MarkName = markName;

            Tekla.Structures.Drawing.View sectionView;
            Tekla.Structures.Drawing.SectionMark sectionMark;

            Tekla.Structures.Drawing.View.CreateSectionView(
                elevationView,
                p1,
                p2,
                insertionPoint,
                depthUp,
                depthDown,
                viewAttr,
                markAttr,
                out sectionView,
                out sectionMark);
        }
    }
}
