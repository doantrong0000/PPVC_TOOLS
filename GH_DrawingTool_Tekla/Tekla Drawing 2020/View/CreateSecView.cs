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
using Tekla.Structures.Model;

namespace GH_Tekla_Dwg.Drawing
{
    public class CreateSecView : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public CreateSecView()
          : base("Create a section view", "create a section view",
              "Create a Tekla section view",
              "Tekla Drawing 2020", "View")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);
            pManager.AddPointParameter("Section mark 1st point", "SecMark 1st point", "add a the 1st point of section mark", GH_ParamAccess.item);
            pManager.AddPointParameter("Section mark 2nd point", "SecMark 2nd point", "add a the 2nd point of section mark", GH_ParamAccess.item);
            pManager.AddPointParameter("Point for Section Mark position", "SecMark position", "add a point to place the section view", GH_ParamAccess.item);
            pManager.AddNumberParameter("View up", "view up", "add a value for the view depth up", GH_ParamAccess.item);
            pManager.AddNumberParameter("View down", "view down", "add a value for the view depth down", GH_ParamAccess.item);
            pManager.AddTextParameter("Section view property", "secView property", "add the section view property", GH_ParamAccess.item);
            pManager.AddTextParameter("Section view mark property", "secView mark property", "add the section view mark property", GH_ParamAccess.item);
            pManager.AddTextParameter("Section view mark name ", "secView mark name", "add the section view mark name", GH_ParamAccess.item);
           

        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            pManager.Register_GenericParam("Section view", "section view", "the created section view", GH_ParamAccess.item);
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            TSD.View containView = null;
            Point3d secMark1stPoint = Point3d.Unset;
            Point3d secMark2ndPoint = Point3d.Unset;
            Point3d secViewPosition = Point3d.Unset;
            double viewUp = 0;
            double viewDown = 0;
            string viewAttribute = "";
            string sectionMarkAttribute = "";
            string sectionMarkName = "";
        



            // Load values from inputs into those variables
            if (!DA.GetData(0, ref containView)) return;
            if (!DA.GetData(1, ref secMark1stPoint)) return;
            if (!DA.GetData(2, ref secMark2ndPoint)) return;
            if (!DA.GetData(3, ref secViewPosition)) return;
            if (!DA.GetData(4, ref viewUp)) return;
            if (!DA.GetData(5, ref viewDown)) return;
            if (!DA.GetData(6, ref viewAttribute)) return;
            if (!DA.GetData(7, ref sectionMarkAttribute)) return;
            if (!DA.GetData(8, ref sectionMarkName)) return;



            //The code
            TSM.Model model = new TSM.Model();
            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();

            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            TSD.View sel_view = containView as TSD.View;
            TSM.WorkPlaneHandler wph = model.GetWorkPlaneHandler();
            TSM.TransformationPlane currentPlane = wph.GetCurrentTransformationPlane();
            TSM.TransformationPlane viewPlane = new TransformationPlane(containView.DisplayCoordinateSystem);

            T3D.Point sec_mark_sp = new T3D.Point(secMark1stPoint.X, secMark1stPoint.Y, secMark1stPoint.Z);
            T3D.Point sec_mark_ep = new T3D.Point(secMark2ndPoint.X, secMark2ndPoint.Y, secMark2ndPoint.Z);
            T3D.Point sec_view_p = new T3D.Point(secViewPosition.X, secViewPosition.Y, secViewPosition.Z);


            T3D.Point new_sec_mark_sp = viewPlane.TransformationMatrixToLocal.Transform(currentPlane.TransformationMatrixToGlobal.Transform(sec_mark_sp));
            T3D.Point new_sec_mark_ep = viewPlane.TransformationMatrixToLocal.Transform(currentPlane.TransformationMatrixToGlobal.Transform(sec_mark_ep));
            //T3D.Point new_sec_view_p = viewPlane.TransformationMatrixToLocal.Transform(currentPlane.TransformationMatrixToGlobal.Transform(sec_view_p));

            double d_up = viewUp;
            double d_down = viewDown;
            TSD.View.ViewAttributes v_att = new TSD.View.ViewAttributes(viewAttribute);
            
            TSD.SectionMarkBase.SectionMarkAttributes sec_mark = new TSD.SectionMarkBase.SectionMarkAttributes();
            sec_mark.LoadAttributes(sectionMarkAttribute);
            TSD.View sec_view = null;
            TSD.SectionMark sec_mark_return = null;

            TSD.View.CreateSectionView(sel_view, new_sec_mark_sp, new_sec_mark_ep, sec_view_p, d_up, d_down, v_att, sec_mark, out sec_view, out sec_mark_return);

            sec_mark_return.Attributes.MarkName = sectionMarkName;
            sec_view.Attributes.LoadAttributes(viewAttribute);
            sec_mark_return.Modify();
            sec_view.Modify();
            dr.CommitChanges();
           
            // Output
            DA.SetData(0, sec_view);

          
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.section_view;
        
        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("00F1AC8A-1B14-49E0-BB10-8AB8DDFEDD68"); }
        }
    }
}