using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Rhino.Geometry;

//Namespace for Tekla
using TSM = Tekla.Structures.Model;
using TSMUI = Tekla.Structures.Model.UI;
using T3D = Tekla.Structures.Geometry3d;
using TSDUI = Tekla.Structures.Drawing.UI;
using TSD = Tekla.Structures.Drawing;
using TSS = Tekla.Structures.Solid;
using Eto.Forms;
using Rhino.Collections;
using System.Collections;
using Tekla.Structures.Model;
using Tekla.Structures.Drawing;

namespace GH_Tekla_Dwg.Drawing
{
    public class ArrangeRebarMark : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ArrangeRebarMark()
          : base("Arrange rebar mark", "arrange rebar mark",
              "arrange rebar mark",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
           
            
            pManager.AddGenericParameter("Rebar mark arraylist", "Rebar mark list", "add a rebar mark list", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offset X", "offset X", "add the offset X direction", GH_ParamAccess.item);
            pManager.AddNumberParameter("Offset Y", "offset Y", "add the offset Y direction", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            
        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables

            ArrayList rebMarkList = new ArrayList();
            double offSetX = 0;
            double offSetY = 0;

            // Load values from inputs into those variables

            if (!DA.GetData(0, ref rebMarkList)) return;
            if (!DA.GetData(1, ref offSetX)) return;
            if (!DA.GetData(2, ref offSetY)) return;

            
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();
          

            foreach (TSD.Mark rebMark1 in rebMarkList)
            {
                TSD.LeaderLinePlacing yea = rebMark1.Placing as TSD.LeaderLinePlacing;
               
                rebMark1.InsertionPoint.X = yea.StartPoint.X + offSetX;
                rebMark1.InsertionPoint.Y = yea.StartPoint.Y + offSetY;
                rebMark1.Modify();  
                dr.CommitChanges();
            }
            //// Outputs
            
          
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.arrange_rebar_mark;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("59D3CB4A-468F-4D52-AB88-2CF8D948D470"); }
        }
    }
}