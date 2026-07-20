using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PPVCREVIT.Utils
{
    public static class UnitUtils
    {
        public static double FeetToMm(this double feet)
        {
            return feet * 304.8;
        }

        public static double FeetToMm(this int feet)
        {
            return feet * 304.8;
        }

        public static double MmToFeet(this double mm)
        {
            return mm / 304.8;
        }

        public static double MmToFeet(this int mm)
        {
            return mm / 304.8;
        }
    }
}
