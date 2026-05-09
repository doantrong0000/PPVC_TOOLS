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

namespace GH_Tekla_Dwg
{
    public class HideDwgRebarByName : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public HideDwgRebarByName()
          : base("Hide Drawing Rebar Filtered by Name", "hide dwg rebar filtered by name",
              "hide Tekla drawing rebar filtered by Name",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name of Rebar groups", "rebar group name", "add the rebar group name", GH_ParamAccess.item);
            pManager.AddGenericParameter("Contain view", "contain view", "add a contain view", GH_ParamAccess.item);
            pManager.AddGenericParameter("Model rebar arraylist", "model rebar list", "add a model rebar list", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Select or not", "select rebar or not", "option for selecting", GH_ParamAccess.item, false);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Filtered model rebar arraylist", "filtered model rebar list", "the filtered model rebar just got", GH_ParamAccess.item);
            pManager.Register_GenericParam("Filtered drawing rebar arraylist", "filtered drawing rebar list", "the filtered drawing rebar just got", GH_ParamAccess.item);
        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            string rebarName = "";
            TSD.View dwgView = null;
            ArrayList modelRebarList = new ArrayList();
            bool selectRebar = false;


            // Load values from inputs into those variables
            if (!DA.GetData(0, ref rebarName)) return;
            if (!DA.GetData(1, ref dwgView)) return;
            if (!DA.GetData(2, ref modelRebarList)) return;
            if (!DA.GetData(3, ref selectRebar)) return;

            TSD.DrawingObjectEnumerator rebarEnum2 = dwgView.GetAllObjects();

            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            // Construct 3 arraylist for rebar in model and drawing
            ArrayList modelFilterRebarList = new ArrayList();
            ArrayList dwgFilterRebarList = new ArrayList();

            //Construct model rebar group filtered by name in model
            RebarGroup modelFilterRebar = null;
            //Construct drawing rebar group filtered by name in drawing
            TSD.ReinforcementGroup dwgFilterRebar = null;

            // Check through model rebar list (to get a list of model rebar filtered by name)
            foreach (RebarGroup modelRebarG in modelRebarList)
            {
                if (modelRebarG.Name.ToString() == rebarName)
                {
                    modelFilterRebar = modelRebarG;
                    modelFilterRebarList.Add(modelFilterRebar);
                }
            }
            //MessageBox.Show(modelFilterRebar.Name.ToString());
            //MessageBox.Show(modelFilterRebarList.Count.ToString());

            while (rebarEnum2.MoveNext())
            {
                foreach (RebarGroup filterModelRebar in modelFilterRebarList)
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

            //MessageBox.Show(dwgFilterRebar.ModelIdentifier.ToString());
            //MessageBox.Show(dwgFilterRebarList.Count.ToString());
            // Outputs

            DA.SetData(0, modelFilterRebarList);
            DA.SetData(1, dwgFilterRebarList);

            if (selectRebar == false)
            {
                dh.GetDrawingObjectSelector().UnselectAllObjects();
                dr.CommitChanges();
            }
            else if (selectRebar == true)
            {
                dh.GetDrawingObjectSelector().SelectObjects(dwgFilterRebarList, true);
            }

        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.hide_rebar;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("59D3CBDA-468F-4D55-AB88-2CF8D948D474"); }
        }
    }
}