using System;
using System.Collections.Generic;
using System.Linq;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;

namespace TeklaApp.Models
{
    public class RebarNumberingModel
    {
        public string GetRebarSignature(Reinforcement rebar)
        {
            // 1. Name
            string name = rebar.Name ?? "";

            // 2. Size (Diameter)
            string size = "";
            rebar.GetReportProperty("SIZE", ref size);

            // 3. Length (Total Length)
            double length = 0;
            rebar.GetReportProperty("LENGTH", ref length);
            string lengthStr = Math.Round(length, 0).ToString();

            // 4. Shape (Geometric Key)
            string shapeKey = GetShapeKey(rebar);

            // 5. Hooks
            string hookKey = GetHookKey(rebar);

            // Filter/Grouping Rule: Identical name, size, hook, length, shape
            return $"{name}|{size}|{lengthStr}|{shapeKey}|{hookKey}";
        }

        public void AutoAssignPrefix(Reinforcement rebar, Part hostPart, string slabKeys = "SLAB,SÀN,FLOOR", string beamKeys = "TB,DẦM,BEAM", string wallKeys = "TW,SW,VÁCH,WALL")
        {
            string prefix = "";
            
            if (hostPart != null)
            {
                string hostName = (hostPart.Name ?? "").ToUpper();
                bool isSlab = MatchesKeywords(hostName, slabKeys) || hostPart is ContourPlate;
                bool isWall = MatchesKeywords(hostName, wallKeys);
                bool isBeam = MatchesKeywords(hostName, beamKeys);

                if (isSlab)
                {
                    // For Slabs: Prefix depends on the REBAR orientation
                    prefix = GetRebarDirectionPrefix(rebar, false);
                }
                else if (isWall)
                {
                    // For Walls: Prefix depends on REBAR orientation (Z=V, X/Y=H)
                    prefix = GetRebarDirectionPrefix(rebar, true);
                }
                else
                {
                    // For Beams and others fallback: Prefix depends on the HOST orientation
                    prefix = GetPartDirectionPrefix(hostPart);
                }
            }
            else
            {
                // Fallback to rebar orientation if no host provided
                prefix = GetRebarDirectionPrefix(rebar, false);
            }

            if (!string.IsNullOrEmpty(prefix))
            {
                if (rebar.NumberingSeries != null)
                {
                    rebar.NumberingSeries.Prefix = prefix;
                    rebar.Modify();
                }
            }
        }

        private bool MatchesKeywords(string name, string keywords)
        {
            if (string.IsNullOrWhiteSpace(keywords)) return false;
            var keys = keywords.Split(',').Select(k => k.Trim().ToUpper()).Where(k => !string.IsNullOrEmpty(k));
            foreach (var key in keys)
            {
                if (name.Contains(key)) return true;
            }
            return false;
        }

        private string GetDirectionFromVector(Vector vec, bool isWall = false)
        {
            vec.Normalize();
            double absX = Math.Abs(vec.X);
            double absY = Math.Abs(vec.Y);
            double absZ = Math.Abs(vec.Z);

            // Tolerance: approx 10 degrees (cos(10deg) ~ 0.98)
            if (isWall)
            {
                if (absZ > 0.98) return "V"; // Parallel to Z
                if (absX > 0.98 || absY > 0.98) return "H"; // Parallel to X or Y
            }
            else
            {
                if (absX > 0.98) return "H"; // Parallel to X
                if (absY > 0.98) return "V"; // Parallel to Y
            }

            // Diagonal or slanted -> Flag with "X" as requested
            return "X"; 
        }

        private string GetRebarDirectionPrefix(Reinforcement rebar, bool isWall)
        {
            Polygon poly = null;
            if (rebar is RebarGroup group && group.Polygons.Count > 0) poly = group.Polygons[0] as Polygon;
            else if (rebar is SingleRebar single) poly = single.Polygon;

            if (poly == null || poly.Points.Count < 2) return "";

            double maxLength = -1;
            Vector bestVec = null;

            for (int i = 0; i < poly.Points.Count - 1; i++)
            {
                if (poly.Points[i] is Point pA && poly.Points[i + 1] is Point pB)
                {
                    Vector currentVec = new Vector(pB.X - pA.X, pB.Y - pA.Y, pB.Z - pA.Z);
                    double len = currentVec.GetLength();
                    if (len > maxLength)
                    {
                        maxLength = len;
                        bestVec = currentVec;
                    }
                }
            }

            if (bestVec == null || maxLength < 0.1) return "";
            return GetDirectionFromVector(bestVec, isWall);
        }

        private string GetPartDirectionPrefix(Part part)
        {
            Vector vec = null;
            if (part is Beam beam)
            {
                vec = new Vector(beam.EndPoint.X - beam.StartPoint.X, beam.EndPoint.Y - beam.StartPoint.Y, 0);
            }
            else
            {
                // For other parts, use coordinate system main axis
                var cs = part.GetCoordinateSystem();
                vec = cs.AxisX;
            }

            if (vec == null || vec.GetLength() < 0.1) return "";
            return GetDirectionFromVector(vec);
        }

        private string GetShapeKey(Reinforcement rebar)
        {
            System.Collections.ArrayList polygons = null;
            if (rebar is RebarGroup group) polygons = group.Polygons;
            else if (rebar is SingleRebar single) polygons = new System.Collections.ArrayList { single.Polygon };

            if (polygons == null || polygons.Count == 0) return "NoShape";

            List<string> polyKeys = new List<string>();
            foreach (var obj in polygons)
            {
                if (obj is Tekla.Structures.Model.Polygon poly)
                {
                    List<double> lengths = new List<double>();
                    for (int i = 0; i < poly.Points.Count - 1; i++)
                    {
                        var p1 = poly.Points[i] as Tekla.Structures.Geometry3d.Point;
                        var p2 = poly.Points[i + 1] as Tekla.Structures.Geometry3d.Point;
                        double len = Math.Round(Math.Sqrt(Math.Pow(p2.X - p1.X, 2) + Math.Pow(p2.Y - p1.Y, 2) + Math.Pow(p2.Z - p1.Z, 2)), 0);
                        lengths.Add(len);
                    }
                    polyKeys.Add(string.Join("-", lengths));
                }
            }
            return string.Join(";", polyKeys);
        }

        private string GetHookKey(Reinforcement rebar)
        {
            double startHookAngle = 0, endHookAngle = 0;
            double startHookLength = 0, endHookLength = 0;

            rebar.GetReportProperty("HOOK_START_ANGLE", ref startHookAngle);
            rebar.GetReportProperty("HOOK_END_ANGLE", ref endHookAngle);
            rebar.GetReportProperty("HOOK_START_LENGTH", ref startHookLength);
            rebar.GetReportProperty("HOOK_END_LENGTH", ref endHookLength);

            return $"S:{Math.Round(startHookAngle, 0)}-{Math.Round(startHookLength, 0)}|E:{Math.Round(endHookAngle, 0)}-{Math.Round(endHookLength, 0)}";
        }
    }
}
