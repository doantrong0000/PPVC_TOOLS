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
    public class GetFilteredDwgRebarByPrefix_old : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetFilteredDwgRebarByPrefix_old()
          : base("Get Drawing Rebar Filtered by Prefix", "get DWG rebar filtered by prefix",
              "Get Tekla drawing rebar filtered by prefix",
              "", "")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);
            pManager.AddTextParameter("Rebar group prefix", "rebar group prefix", "add the rebar group prefix", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Drawing rebar arraylist", "drawing rebar list", "the drawing rebar just got", GH_ParamAccess.item);
            pManager.Register_GenericParam("Model rebar arraylist", "model rebar list", "the model rebar just got", GH_ParamAccess.item);
            pManager.Register_GenericParam("Filtered drawing rebar arraylist", "filtered drawing rebar list", "the filtered drawing rebar just got", GH_ParamAccess.item);
            pManager.Register_GenericParam("Filtered model rebar arraylist", "filtered model rebar list", "the filtered model rebar just got", GH_ParamAccess.item);
        }
    

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            TSD.View dwgView = null;
            string rebarPrefix = "";

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref dwgView)) return;
            if (!DA.GetData(1, ref rebarPrefix)) return;

            //Connect with model environment
            TSM.Model model = new TSM.Model();
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            // Get all objects in the chose view
            TSD.DrawingObjectEnumerator rebarEnum1= dwgView.GetAllObjects();
            TSD.DrawingObjectEnumerator rebarEnum2 = dwgView.GetAllObjects();

            // Construct 3 arraylist for rebar in model and drawing
            ArrayList modelRebarList = new ArrayList();
            ArrayList dwgRebarList = new ArrayList();
            ArrayList modelFilterRebarList = new ArrayList();
            ArrayList dwgFilterRebarList = new ArrayList();

            //Construct model rebar group 
            TSM.RebarGroup modelRebar = null;
            //Construct drawing rebar group =
            TSD.ReinforcementGroup dwgRebar = null;
            //Construct model rebar group filtered by name in model
            TSM.RebarGroup modelFilterRebar = null;
            //Construct drawing rebar group filtered by name in drawing
            TSD.ReinforcementGroup dwgFilterRebar = null;

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
           
            // Check through model rebar list (to get a list of model rebar filtered by name)
            foreach (TSM.RebarGroup modelRebarG in modelRebarList)
            {
                if (modelRebarG.NumberingSeries.Prefix.ToString() == rebarPrefix)
                {
                    modelFilterRebar = modelRebarG;
                    modelFilterRebarList.Add(modelFilterRebar);
                }
            }
                //MessageBox.Show(modelFilterRebarList.Count.ToString());

            while (rebarEnum2.MoveNext())
            {
                foreach (TSM.RebarGroup filterModelRebar in modelFilterRebarList)
                {
                    if (rebarEnum2.Current is TSD.ReinforcementGroup)
                    {
                        dwgFilterRebar = rebarEnum2.Current as TSD.ReinforcementGroup;

                        if (dwgFilterRebar.ModelIdentifier.ToString() == filterModelRebar.Identifier.ToString())
                        {
                            dwgFilterRebarList.Add(dwgFilterRebar);
                        }
                    }

                }
            }
            
            // Outputs
            DA.SetData(0, dwgRebarList);
            DA.SetData(1, modelRebarList);
            DA.SetData(2, modelFilterRebarList);
            DA.SetData(3, dwgFilterRebarList);
            
            dh.GetDrawingObjectSelector().SelectObjects(dwgFilterRebarList, true);
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon
        {
            get
            {
                //You can add image files to your project resources and access them like this:
                // return Resources.IconForThisComponent;
                return null;
            }
        }

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("59D3CBDA-468F-4D55-AB88-2CF8D948D471"); }
        }
    }
}