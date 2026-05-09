using Grasshopper;
using Grasshopper.Kernel;
using System;
using System.Drawing;

namespace GH_Tekla_Dwg
{
    public class GH_Tekla_DwgInfo : GH_AssemblyInfo
    {
        public override string Name => "GH_Tekla_Dwg";

        //Return a 24x24 pixel bitmap to represent this GHA library.
        public override Bitmap Icon => null;

        //Return a short string describing the purpose of this GHA library.
        public override string Description => "";

        public override Guid Id => new Guid("3b5e4ff8-1b13-4355-b03d-3b7db37b112e");

        //Return a string identifying you or your company.
        public override string AuthorName => "";

        //Return a string representing your preferred contact details.
        public override string AuthorContact => "";
    }
}