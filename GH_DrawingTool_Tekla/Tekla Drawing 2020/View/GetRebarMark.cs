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
    public class GetRebarMark : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public GetRebarMark()
          : base("Get rebar mark", "Get rebar mark",
              "Get rebar mark",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
           
            pManager.AddGenericParameter("Contain view", "contain view", "add a contain view",GH_ParamAccess.item);
            pManager.AddPointParameter("Rhino Points", "Rhino points", "add the PPVC central point", GH_ParamAccess.item);
          
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Rebar mark with X min Y max", "Rebar mark with X min Y max", "Rebar mark with X min Y max", GH_ParamAccess.item);
            pManager.Register_GenericParam("Rebar mark with X min Y min", "Rebar mark with X min Y min", "Rebar mark with X min Y min", GH_ParamAccess.item);
            pManager.Register_GenericParam("Rebar mark with X max Y max", "Rebar mark with X max Y max", "Rebar mark with X max Y max", GH_ParamAccess.item);
            pManager.Register_GenericParam("Rebar mark with X max Y min", "Rebar mark with X max Y min", "Rebar mark with X max Y min", GH_ParamAccess.item);

        }
        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            
            TSD.View dwgView = null;       
     
            Point3d centralPoint = new Point3d();
            

            // Load values from inputs into those variables

            if (!DA.GetData(0, ref dwgView)) return;
            if (!DA.GetData(1, ref centralPoint)) return;
        

            TSM.Model model = new TSM.Model();
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            TSM.WorkPlaneHandler wph = model.GetWorkPlaneHandler();
            TSM.TransformationPlane currentPlane = wph.GetCurrentTransformationPlane();
            TSM.TransformationPlane viewPlane = new TransformationPlane(dwgView.DisplayCoordinateSystem);

            T3D.Point teklaCentralPoint = new T3D.Point(centralPoint.X, centralPoint.Y, centralPoint.Z);
            T3D.Point newTeklaPoint = viewPlane.TransformationMatrixToLocal.Transform(currentPlane.TransformationMatrixToGlobal.Transform(teklaCentralPoint));

            // Get all objects in the chose view
            TSD.DrawingObjectEnumerator rebMarkEnum1 = dwgView.GetAllObjects();

            // Construct 3 arraylist for rebar in model and drawing
            ArrayList rebMarkList = new ArrayList();
            ArrayList rebMarkListXminYmax = new ArrayList();
            ArrayList rebMarkListXminYmin = new ArrayList();
            ArrayList rebMarkListXmaxYmax = new ArrayList();
            ArrayList rebMarkListXmaxYmin = new ArrayList();


            //Construct drawing rebar group
            TSD.Mark rebMark1 = null;

            T3D.Point pl = new T3D.Point();
            // Check through the rebar enumaration
            while (rebMarkEnum1.MoveNext())
            {
                // if drawing object is the drawing rebar group type
                if (rebMarkEnum1.Current is TSD.Mark)
                {
                    // Cast drawing object to drawing rebar group
                    rebMark1 = rebMarkEnum1.Current as TSD.Mark;
                    // Add model rebar group to model rebar list
                    rebMarkList.Add(rebMark1); 
                }
            }

            foreach (TSD.Mark rebMark2 in rebMarkList)
            {
                TSD.LeaderLinePlacing yea = rebMark2.Placing as TSD.LeaderLinePlacing;
                if (yea != null && yea.StartPoint.X < newTeklaPoint.X && yea.StartPoint.Y >0)
                {
                    rebMarkListXminYmax.Add(rebMark2);
                }
            }
            
            foreach (TSD.Mark rebMark3 in rebMarkList)
            {
                TSD.LeaderLinePlacing yea = rebMark3.Placing as TSD.LeaderLinePlacing;
                if (yea != null && yea.StartPoint.X < newTeklaPoint.X && yea.StartPoint.Y <0)
                {
                    rebMarkListXminYmin.Add(rebMark3);
                }
            }

            foreach (TSD.Mark rebMark4 in rebMarkList)
            {
                TSD.LeaderLinePlacing yea = rebMark4.Placing as TSD.LeaderLinePlacing;
                if (yea != null && yea.StartPoint.X > newTeklaPoint.X && yea.StartPoint.Y > 0)
                {
                    rebMarkListXmaxYmax.Add(rebMark4);
                }
            }

            foreach (TSD.Mark rebMark5 in rebMarkList)
            {
                TSD.LeaderLinePlacing yea = rebMark5.Placing as TSD.LeaderLinePlacing;
                if (yea != null && yea.StartPoint.X > newTeklaPoint.X && yea.StartPoint.Y < 0)
                {
                    rebMarkListXmaxYmin.Add(rebMark5);
                }
            }

            dh.GetDrawingObjectSelector().SelectObjects(rebMarkListXmaxYmin, true);

            // Outputs
            DA.SetData(0, rebMarkListXminYmax);
            DA.SetData(1, rebMarkListXminYmin);
            DA.SetData(2, rebMarkListXmaxYmax);
            DA.SetData(3, rebMarkListXmaxYmin);
        }
        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.rebar_name;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("59D3CB4A-468F-4D55-AB88-2CF8D948D470"); }
        }
    }
}