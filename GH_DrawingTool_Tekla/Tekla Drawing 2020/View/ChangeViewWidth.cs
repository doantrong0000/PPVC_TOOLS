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
using Tekla.Structures.Geometry3d;

namespace GH_Tekla_Dwg.Drawing
{
    public class ChangeViewWidth : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ChangeViewWidth()
          : base("Change view width", "change view width",
              "change view width",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);    
            pManager.AddNumberParameter("Max view width", "max width", "add max width", GH_ParamAccess.item);
            pManager.AddNumberParameter("Min view width", "min width", "add min width", GH_ParamAccess.item);        
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            //pManager.Register_GenericParam("Section view", "section view", "the created section view", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            TSD.View containView = null;
            double maxWidth = 0;
            double minWidth = 0;

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref containView)) return;          
            if (!DA.GetData(1, ref maxWidth)) return;
            if (!DA.GetData(2, ref minWidth)) return;
           

            //The code
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();

            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            TSD.View sel_view = containView as TSD.View;
                            
            sel_view.RestrictionBox.MaxPoint.X = maxWidth;
            sel_view.RestrictionBox.MinPoint.X = minWidth * -1;

            sel_view.Modify();
            dr.CommitChanges();
            // Output
            //DA.SetData(0, sec_view);

        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.view_width;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("00F1AC82-1B14-49E0-3B10-8AB9DDFEDD68"); }
        }
    }
}