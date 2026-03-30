using System;
using System.Collections.Generic;

namespace TeklaApp.Models
{
    public class AppSettings
    {
        // RebarToolsPage
        public string Spacing { get; set; } = "200";
        public string StartOffset { get; set; } = "25";
        public string EndOffset { get; set; } = "25";
        public string OnPlaneOffset { get; set; } = "25";
        public string RebarName { get; set; } = "REBAR";
        public string RebarSize { get; set; } = "10";
        public string RebarGrade { get; set; } = "H";
        public string RebarClass { get; set; } = "2";
        public bool MergeGroups { get; set; } = true;

        // JoinAssemblyPage
        public bool JoinSteel { get; set; } = true;
        public bool JoinEmbed { get; set; } = true;
        public bool JoinRebar { get; set; } = true;
        public bool JoinBolt { get; set; } = true;
        public bool JoinWeld { get; set; } = false;
        public bool JoinSurface { get; set; } = true;
        public bool JoinFeatures { get; set; } = false;

        // ViewAlignPage
        public string AlignAxis { get; set; } = "Z"; // X, Y, Z
        public string AlignMode { get; set; } = "EditPoints"; // EditPoints, MoveObject, DrawingView
        public bool AlignByCenter { get; set; } = false;

        // StepTagPage
        public string StepTextHeight { get; set; } = "3.5";
        public string StepFontName { get; set; } = "Arial";
        public string StepTextColor { get; set; } = "Green";
        public string StepSurfLen { get; set; } = "15";
        public string StepHeight { get; set; } = "10";
        public string StepHatchSpc { get; set; } = "3";
        public string StepHatchLen { get; set; } = "12";
        public bool StepUseRectFill { get; set; } = false;
        public string StepFillName { get; set; } = "ANSI31_13";
        public string StepScaleX { get; set; } = "1";
        public string StepScaleY { get; set; } = "1";

        // RebarNumberingPage
        public string SlabKeywords { get; set; } = "SLAB, sàn";
        public string BeamKeywords { get; set; } = "BEAM, dầm, TB";
        public string WallKeywords { get; set; } = "WALL, tường, SW, TW";
        public string StartingNumber { get; set; } = "1";
        public string SizeClassMapping { get; set; } = "8:1;10:2;12:3;13:3;14:4;16:5;18:6;20:7;22:8;25:9;28:10;32:11";

        // RebarFinderPage
        public string FinderAttributeName { get; set; } = "REBAR_SEQ_NO";
    }
}
