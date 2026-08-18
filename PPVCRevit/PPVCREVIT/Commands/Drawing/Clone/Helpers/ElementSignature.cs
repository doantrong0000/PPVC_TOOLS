using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;

namespace PPVCREVIT.Commands.Drawing.Clone.Helpers
{
    public class ElementSignature
    {
        public ElementId CategoryId { get; private set; }
        public ElementId TypeId { get; private set; }
        
        public double Volume { get; private set; }
        public double SurfaceArea { get; private set; }
        
        public double TotalLength { get; private set; }
        public ElementId RebarShapeId { get; private set; }

        public bool IsRebar { get; private set; }

        public ElementSignature(Element elem)
        {
            CategoryId = elem.Category?.Id ?? ElementId.InvalidElementId;
            TypeId = elem.GetTypeId();

            if (elem is Rebar rebar)
            {
                IsRebar = true;
                RebarShapeId = rebar.GetShapeId();
                Parameter lengthParam = rebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH);
                if (lengthParam != null)
                {
                    TotalLength = Math.Round(lengthParam.AsDouble(), 3);
                }
            }
            else
            {
                IsRebar = false;
                ExtractLargestSolidInfo(elem);
            }
        }

        private void ExtractLargestSolidInfo(Element elem)
        {
            Options opt = new Options();
            opt.DetailLevel = ViewDetailLevel.Fine;
            GeometryElement geomElem = elem.get_Geometry(opt);
            if (geomElem == null) return;

            Solid largestSolid = null;
            double maxVolume = -1;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Faces.Size > 0 && solid.Volume > 0)
                {
                    if (solid.Volume > maxVolume)
                    {
                        maxVolume = solid.Volume;
                        largestSolid = solid;
                    }
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid instSolid && instSolid.Faces.Size > 0 && instSolid.Volume > 0)
                        {
                            if (instSolid.Volume > maxVolume)
                            {
                                maxVolume = instSolid.Volume;
                                largestSolid = instSolid;
                            }
                        }
                    }
                }
            }

            if (largestSolid != null)
            {
                Volume = Math.Round(largestSolid.Volume, 3);
                SurfaceArea = Math.Round(largestSolid.SurfaceArea, 3);
            }
        }

        public bool IsIdenticalTo(ElementSignature other)
        {
            if (other == null) return false;

            if (CategoryId != other.CategoryId || TypeId != other.TypeId)
                return false;

            if (IsRebar && other.IsRebar)
            {
                return RebarShapeId == other.RebarShapeId && Math.Abs(TotalLength - other.TotalLength) < 1e-3;
            }
            else if (!IsRebar && !other.IsRebar)
            {
                return Math.Abs(Volume - other.Volume) < 1e-3 && Math.Abs(SurfaceArea - other.SurfaceArea) < 1e-3;
            }

            return false;
        }
    }
}
