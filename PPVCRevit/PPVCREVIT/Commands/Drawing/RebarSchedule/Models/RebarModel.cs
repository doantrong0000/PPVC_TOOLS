using System;

namespace PPVCREVIT.Commands.Drawing.RebarSchedule.Models
{
    public class RebarModel
    {
        public int No { get; set; }
        public string Id { get; set; }
        public string WhRebarType { get; set; }
        public string WhRebarPrefix { get; set; } = string.Empty;
        public string RebarNumber { get; set; }
        public string TypeName { get; set; }
        public int TagCount { get; set; }

        public string Status => TagCount > 0 ? $"✓ Tagged ({TagCount})" : "✗ Untagged";
        public bool IsTagged => TagCount > 0;
    }
}
