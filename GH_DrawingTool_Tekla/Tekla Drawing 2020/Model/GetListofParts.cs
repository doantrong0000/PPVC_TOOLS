using System;
using System.Collections.Generic;

using Grasshopper.Kernel;
using Grasshopper.Kernel.Types;
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
using Grasshopper.Kernel.Types;
using Tekla.Structures.Model;
using Tekla.Structures.Geometry3d;
using System.Collections;

namespace GH_Tekla_Dwg.Tekla_Drawing_2020.View
{
    public class GetListofParts : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetListofParts()
          : base("Get secondary Tekla parts from an Assembly", "Get all Tekla parts of an assembly",
              "Get all secondary parts",
              "Tekla Drawing 2020", "Model")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tekla Assembly", "Tekla assembly ", "add a Tekla assembly", GH_ParamAccess.item);
            pManager.AddTextParameter("Part name", "part name", "add the part name", GH_ParamAccess.item);
        }
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
           
            //pManager.Register_PointParam("Rhino Points", "Thino points", "add a list of Rhino Points", GH_ParamAccess.list);
            pManager.Register_GenericParam("Secondary parts", "parts", "list of Tekla parts", GH_ParamAccess.list);
            pManager.Register_GenericParam("Main part", "main part", "Tekla main part", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            
            // Degine placeholder variables

            TSM.Assembly teklaPart = null;  
            string partName = "";

            // Load values from inputs into those variables
           
            if (!DA.GetData(0, ref teklaPart)) return;     
            if (!DA.GetData(1, ref partName)) return;

            
            //Connect with model environment
            TSM.Model activemodel = new TSM.Model();

            TSM.Assembly mAssem = teklaPart as TSM.Assembly;

            ArrayList partList = mAssem.GetSecondaries();

            TSM.ModelObject mainP = mAssem.GetMainPart();  
           

            List<TSM.Part> partList1 = new List<TSM.Part>();
            foreach (TSM.Part part in partList)
            {
                if (part.Name.ToString() == partName)
                {
                    partList1.Add(part);
                }

            }

            // Outputs
            DA.SetDataList(0, partList1);
            DA.SetData(1, mainP);

        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.get_2nd_parts;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3C055A2C-10CD-4A07-BA1C-566A8516CE56"); }
        }
    }
}