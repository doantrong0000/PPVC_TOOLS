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
using Tekla.Structures.DrawingInternal;

namespace GH_Tekla_Dwg.Drawing
{
    public class GetView : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetView()
          : base("Get a View", "GetView",
              "Get a Tekla view",
              "Tekla Drawing 2020", "View")
        {
        }


        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddBooleanParameter("Disable", "D", "If true, the component will be disabled", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Tekla View", "view", "Drawing View", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// 
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        /// 

        protected override void SolveInstance(IGH_DataAccess DA)
        {


            bool disable = false;
            if (!DA.GetData(0, ref disable)) return;
          
            if (disable == true)
            {
                //The code
               
                //Connect with drawing environment
                TSD.DrawingHandler dh = new TSD.DrawingHandler();

                // Working with active drawing
                TSD.Drawing dr = dh.GetActiveDrawing();

                // Indicate Picker event
                TSDUI.Picker myPicker = dh.GetPicker();

                TSD.DrawingObject drobj = null;
                TSD.ViewBase viewbase = null;

                myPicker.PickObject("Select a Tekla view", out drobj, out viewbase);

                // Indicate a view
                TSD.View view = viewbase as TSD.View;

                // Outputs
                DA.SetData(0, view);
            }      
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.get_view;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1564D7F6-A4C8-4486-98F0-0EB4F6C7B602"); }
        }
    }
}