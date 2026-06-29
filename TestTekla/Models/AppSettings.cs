using System;
using System.Collections.Generic;

namespace TeklaApp.Models
{
    public class AppSettings
    {

        public string SizeClassMapping { get; set; } = "8:1;10:2;12:3;13:3;14:4;16:5;18:6;20:7;22:8;25:9;28:10;32:11";

        // Overlap Detection
        public double OverlapLengthTolerance { get; set; } = 10.0;
    }
}
