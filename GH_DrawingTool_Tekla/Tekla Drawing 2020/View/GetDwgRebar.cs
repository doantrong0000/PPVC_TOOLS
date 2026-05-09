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

namespace GH_Tekla_Dwg.Drawing
{
    public class GetDwgRebar : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetDwgRebar()
          : base("Get All Drawing Rebar in View", "get dwg rebar",
              "Get Tekla drawing rebar",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);
            
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Model rebar arraylist", "model rebar list", "the model rebar just got", GH_ParamAccess.item);
            pManager.Register_GenericParam("Drawing rebar arraylist", "drawing rebar list", "the drawing rebar just got", GH_ParamAccess.item);
        }
    

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            TSD.View dwgView = null;
            

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref dwgView)) return;
            

            //Connect with model environment
            TSM.Model model = new TSM.Model();
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            // Get all objects in the chose view
            TSD.DrawingObjectEnumerator rebarEnum1= dwgView.GetAllObjects();  

            // Construct 4 arraylist for rebar in model and drawing
            ArrayList modelRebarList = new ArrayList();
            ArrayList dwgRebarList = new ArrayList();
            

            //Construct model rebar group 
            TSM.RebarGroup modelRebar = null;
            //Construct drawing rebar group =
            TSD.ReinforcementGroup dwgRebar = null;
            
            // Check through the rebar enumaration
            while (rebarEnum1.MoveNext())
            {
                // if drawing object is the drawing rebar group type
                if (rebarEnum1.Current is TSD.ReinforcementGroup)
                {
                    // Cast drawing object to drawing rebar group
                    dwgRebar = rebarEnum1.Current as TSD.ReinforcementGroup;


                    //Get rebar group in model based on drawing rebar group in drawing by indentity
                    modelRebar = model.SelectModelObject(dwgRebar.ModelIdentifier) as TSM.RebarGroup;

                    // Add model rebar group to model rebar list
                    dwgRebarList.Add(dwgRebar);

                    // Add model rebar group to model rebar list
                    modelRebarList.Add(modelRebar);
                }
            }

            // Outputs
            DA.SetData(0, modelRebarList);
            DA.SetData(1, dwgRebarList);

            //dh.GetDrawingObjectSelector().SelectObjects(dwgFilterRebarList, true);
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.dwg_rebar;
    

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("59D3CBDA-468F-4D55-AB88-2CF8D948D472"); }
        }
    }
}