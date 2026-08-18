using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PPVCREVIT.Commands.Drawing.Clone.Helpers
{
    public static class RebarMatchHelper
    {
        public static bool IsMatch(Rebar sourceRebar, Rebar targetRebar)
        {
            if (sourceRebar == null || targetRebar == null) return false;

            // 1. Check Bar Type
            if (sourceRebar.GetTypeId() != targetRebar.GetTypeId())
                return false;

            // 2. Check Shape
            if (sourceRebar.GetShapeId() != targetRebar.GetShapeId())
                return false;

            // 3. Check Total Length
            double sourceLength = sourceRebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH)?.AsDouble() ?? 0;
            double targetLength = targetRebar.get_Parameter(BuiltInParameter.REBAR_ELEM_LENGTH)?.AsDouble() ?? 0;

            // Allow small tolerance for length (e.g. 1mm)
            double tolerance = 1.0 / 304.8; // 1mm in decimal feet
            if (Math.Abs(sourceLength - targetLength) > tolerance)
                return false;

            return true;
        }

        public static Rebar FindMatch(
            Rebar sourceRebar, 
            IEnumerable<Rebar> targetRebars)
        {
            foreach (var target in targetRebars)
            {
                if (IsMatch(sourceRebar, target))
                {
                    return target;
                }
            }

            return null;
        }
    }
}
