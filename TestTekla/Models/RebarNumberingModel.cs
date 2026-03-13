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

        public void AutoAssignPrefix(Reinforcement rebar, Part hostPart)
        {
            string prefix = "";
            
            if (hostPart != null)
            {
                string hostName = (hostPart.Name ?? "").ToUpper();
                bool isSlab = hostName.Contains("SLAB") || hostName.Contains("FLOOR") || hostName.Contains("SÀN") || hostPart is ContourPlate;

                if (isSlab)
                {
                    // For Slabs: Prefix depends on the REBAR orientation
                    prefix = GetRebarDirectionPrefix(rebar);
                }
                else
                {
                    // For Beams/Walls: Prefix depends on the HOST orientation
                    prefix = GetPartDirectionPrefix(hostPart);
                }
            }
            else
            {
                // Fallback to rebar orientation if no host provided
                prefix = GetRebarDirectionPrefix(rebar);
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

        private string GetDirectionFromVector(Vector vec)
        {
            vec.Normalize();
            double absX = Math.Abs(vec.X);
            double absY = Math.Abs(vec.Y);

            // Tolerance: approx 10 degrees (cos(10deg) ~ 0.98)
            if (absX > 0.98) return "H"; // Parallel to X
            if (absY > 0.98) return "V"; // Parallel to Y

            // Diagonal or slanted -> Flag with "X" as requested
            return "X"; 
        }

        private string GetRebarDirectionPrefix(Reinforcement rebar)
        {
            Polygon poly = null;
            if (rebar is RebarGroup group && group.Polygons.Count > 0) poly = group.Polygons[0] as Polygon;
            else if (rebar is SingleRebar single) poly = single.Polygon;

            if (poly == null || poly.Points.Count < 2) return "";

            Point p1 = poly.Points[0] as Point;
            Point p2 = poly.Points[poly.Points.Count - 1] as Point;
            Vector vec = new Vector(p2.X - p1.X, p2.Y - p1.Y, 0);

            if (vec.GetLength() < 0.1) return "";
            return GetDirectionFromVector(vec);
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
