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
    public class GetEdgePointofPart : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetEdgePointofPart()
          : base("Get Tekla part edge points", "Get all edge points of a part",
              "Get all edge points of a part",
              "Tekla Drawing 2020", "Model")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Tekla part", "Tekla part", "add a Tekla part", GH_ParamAccess.item);
            
        }
        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
           
            //pManager.Register_PointParam("Rhino Points", "Thino points", "add a list of Rhino Points", GH_ParamAccess.list);
            pManager.Register_PointParam("Rhino points", "Rhino points", "list of Rhino points", GH_ParamAccess.list);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            
            // Degine placeholder variables

            TSM.Part teklaPart = null;  
            

            // Load values from inputs into those variables
           
            if (!DA.GetData(0, ref teklaPart)) return;


            //Connect with model environment
            TSM.Model model = new TSM.Model();

            teklaPart.Select();
            TSS.EdgeEnumerator edgeenum = teklaPart.GetSolid().GetEdgeEnumerator();

            List<T3D.Point> modelPl = new List<T3D.Point>();
            List<Point3d> rhinoPoints = new List<Point3d>();


            while (edgeenum.MoveNext())
            {
                TSS.Edge edge = edgeenum.Current as TSS.Edge;
                modelPl.Add(edge.StartPoint);
            }

            foreach (T3D.Point edgePoint in modelPl)
            {
                // Convert Rhino.Geometry.Point3d to Tekla.Structures.Geometry3d.Point
                Point3d rhinoPoint = new Point3d(edgePoint.X, edgePoint.Y, edgePoint.Z);

                // Add the converted point to the Tekla PointList
                rhinoPoints.Add(rhinoPoint);
            }

            // Outputs
            DA.SetDataList(0, rhinoPoints);

        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.Edge_Points;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("3C055A2C-10CD-4A07-BA1C-566A1516CE56"); }
        }
    }
}