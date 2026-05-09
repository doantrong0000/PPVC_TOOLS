using System;
using System.Collections.Generic;
using Grasshopper.Kernel;
using Rhino.Geometry;
using Rhino.Collections;


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
using Tekla.Structures.DrawingInternal;

namespace GH_Tekla_Dwg.Drawing
{
    public class SetDimension : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public SetDimension()
          : base("Set a Straight Dimension", "set dim",
              "Create a Tekla straight dimension",
              "Tekla Drawing 2020", "Dimension")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);
            pManager.AddGenericParameter("Tekla part", "Tekla part hosting the set straight dimension", "add a Tekla part hosting the set straight dimension", GH_ParamAccess.item);
            pManager.AddPointParameter("Rhino Points", "Rhino points", "add a list of Rhino Points", GH_ParamAccess.list);
            pManager.AddNumberParameter("Offset", "offset", "add the offset of dimension", GH_ParamAccess.item);
            pManager.AddTextParameter("Dimension Properties", "dimension properties", "add the dimension properties", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Reverse dimension", "reverse dimension", "reverse dimension or not", GH_ParamAccess.item);

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Straight dimension set", "strainght dimension set", "the dimension just created", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            TSD.View containView = null;
            TSM.ModelObject teklaPart = null;
            List<Point3d> rhinoPoints = new List<Point3d>();
            double offSet = 0;
            string dimProperties = "";
            bool reverse = true;

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref containView)) return;
            if (!DA.GetData(1, ref teklaPart)) return;
            if (!DA.GetDataList(2, rhinoPoints)) return;
            if (!DA.GetData(3, ref offSet)) return;
            if (!DA.GetData(4, ref dimProperties)) return;
            if (!DA.GetData(5, ref reverse)) return;


            TSM.Model model = new TSM.Model();
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();
            TSM.WorkPlaneHandler wph = model.GetWorkPlaneHandler();
            TSM.TransformationPlane currentPlane = wph.GetCurrentTransformationPlane();
            TSM.TransformationPlane viewPlane = new TransformationPlane(containView.DisplayCoordinateSystem);
            //wph.SetCurrentTransformationPlane(new TSM.TransformationPlane(containView.DisplayCoordinateSystem));
            //model.CommitChanges();



            TSD.PointList pl = new TSD.PointList();
            TSD.PointList newPl = new TSD.PointList();
            foreach (Point3d rhinoPoint in rhinoPoints)
            {
                // Convert Rhino.Geometry.Point3d to Tekla.Structures.Geometry3d.Point
                T3D.Point teklaPoint = new T3D.Point(rhinoPoint.X, rhinoPoint.Y, rhinoPoint.Z);
                pl.Add(teklaPoint);
              
            }

            foreach (T3D.Point teklaPoint in pl) 
            {
                T3D.Point newTeklaPoint = viewPlane.TransformationMatrixToLocal.Transform(currentPlane.TransformationMatrixToGlobal.Transform(teklaPoint));
                newPl.Add(newTeklaPoint);
                            
            }

            // Get all objects in the chose view
            TSD.DrawingObjectEnumerator dwgObjEnum = containView.GetAllObjects();

            TSD.ModelObject dwgObj = null;    
            while (dwgObjEnum.MoveNext()) 
            {
                if (dwgObjEnum.Current.GetIdentifier().ToString() == teklaPart.Identifier.ToString())
                {
                    dwgObj = dwgObjEnum.Current as TSD.ModelObject;
                }
        
            }
            
            TSD.ViewBase viewbase = containView as TSD.ViewBase;
            T3D.Vector v = new T3D.Vector();

            if (reverse == true)
            {
                v = vectovuong(newPl[0], newPl[1]);
            }
            else if (reverse == false)
            {
                v = vectovuong(newPl[1], newPl[0]);
            }

            TSD.StraightDimensionSet.StraightDimensionSetAttributes sda = new TSD.StraightDimensionSet.StraightDimensionSetAttributes(dwgObj, dimProperties);

            // Create a straight dimension set
            TSD.StraightDimensionSetHandler sdh = new TSD.StraightDimensionSetHandler();
            TSD.StraightDimensionSet sds = sdh.CreateDimensionSet(viewbase, newPl, v, offSet, sda);
            dr.CommitChanges();

            // Outputs
            DA.SetData(0, sds);
            

        }
        T3D.Vector vectovuong(T3D.Point p1, T3D.Point p2)
        {
            T3D.Vector v = new T3D.Vector(p2.X - p1.X, p2.Y - p1.Y, p2.Z - p1.Z);
            T3D.Vector vuong = new T3D.Vector(-p2.Y + p1.Y, p2.X - p1.X, p2.Z - p1.Z);
            return vuong;
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.Dim;
       

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("CD00FE09-B0DD-4F98-BA95-FFAAE1675D94"); }
        }
    }
}