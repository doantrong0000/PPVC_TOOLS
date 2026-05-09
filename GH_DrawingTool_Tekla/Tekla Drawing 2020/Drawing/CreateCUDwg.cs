using System;
using System.Collections;
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

namespace GH_Tekla_Dwg.Drawing
{
    public class CreateCUDwg : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the CreateCUDwg class.
        /// </summary>
        public CreateCUDwg()
          : base("Create a Cast Unit Drawing", "create CU dwg",
              "Create a Tekla Cast Unit Drawing",
              "Tekla Drawing 2020", "Drawing")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddScriptVariableParameter("sheetAttribute", "att", "plug cast unit drawing properties", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Cast Unit Drawing", "CUDwg", "Cast Unit Drawing", GH_ParamAccess.item);
            pManager.Register_GenericParam("Cast Unit Model", "CU", "Cast Unit", GH_ParamAccess.item);
            pManager.Register_GenericParam("Secondary parts", "parts", "list of Tekla parts", GH_ParamAccess.list);

        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            string attr = "";

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref attr)) return;

            // The code
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();

            // Indicate Picker event
            TSMUI.Picker myPicker = new TSMUI.Picker();

            // Get Tekla object need to be created drawing
            TSM.ModelObject mobj = myPicker.PickObject(TSMUI.Picker.PickObjectEnum.PICK_ONE_OBJECT, "Pick a Cast Unit to create Cast Unit Drawing");

            TSM.Assembly mAssem = mobj as TSM.Assembly;

            ArrayList partList = mAssem.GetSecondaries();

            List<TSM.Part> partList1 = new List<TSM.Part>();
            foreach (TSM.Part part in partList)
            {
                    partList1.Add(part);
            }

            if (mobj is TSM.Assembly)
            {  
                TSD.CastUnitDrawing CUDrawing = new TSD.CastUnitDrawing(mobj.Identifier, attr);
                CUDrawing.Insert();

                // Outputs
                DA.SetData(0, CUDrawing);
                DA.SetData(1, mobj);
                DA.SetDataList(2, partList1);
            }
        }


        public override GH_Exposure Exposure => GH_Exposure.primary;
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.CU_dwg;
        

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("1BBF7F32-32E3-4239-8CB3-4C1DDE7694EE"); }
        }
    }
}