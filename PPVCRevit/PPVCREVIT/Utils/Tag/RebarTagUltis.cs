using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Utils.Tag
{
    public static class RebarTagUltis
    {
        public static Reference GetRebarReference(Rebar rebar, View view)
        {
            Options opt = new Options { View = view, ComputeReferences = true };

            GeometryElement geomElem = rebar.get_Geometry(opt);
            if (geomElem != null)
            {
                foreach (GeometryObject geomObj in geomElem)
                {
                    if (geomObj is Curve curve && curve.Reference != null)
                        return curve.Reference;

                    if (geomObj is Solid solid && solid.Faces.Size > 0)
                    {
                        foreach (Face face in solid.Faces)
                        {
                            if (face.Reference != null) return face.Reference;
                        }
                    }

                    if (geomObj is GeometryInstance geomInst)
                    {
                        GeometryElement instGeom = geomInst.GetInstanceGeometry();
                        foreach (GeometryObject instObj in instGeom)
                        {
                            if (instObj is Curve instCurve && instCurve.Reference != null)
                                return instCurve.Reference;

                            if (instObj is Solid instSolid && instSolid.Faces.Size > 0)
                            {
                                foreach (Face face in instSolid.Faces)
                                {
                                    if (face.Reference != null) return face.Reference;
                                }
                            }
                        }
                    }
                }
            }

            return new Reference(rebar);
        }

    }
}
