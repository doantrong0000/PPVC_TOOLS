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
using Rhino.Collections;
using System.Collections;
using Tekla.Structures.Model;
using TSMO = Tekla.Structures.Model.Operations;
using System.IO;

namespace GH_Tekla_Dwg.Tekla_Drawing_2020.Macros
{
    public class ReloadViewProperty : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public ReloadViewProperty()
          : base("Apply Tekla view properties", "Apply view properties",
              "Apply view properties",
              "Tekla Drawing 2020", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
         
            pManager.AddTextParameter("Tekla view properties", "view properties", "add a Tekla view property", GH_ParamAccess.item);
            pManager.AddGenericParameter("Contain view", "view", "add a view", GH_ParamAccess.item);
        }

        /// <summary>
        /// Registers all the output parameters for this component.
        /// </summary>
        protected override void RegisterOutputParams(GH_Component.GH_OutputParamManager pManager)
        {
            
        }

        /// <summary>
        /// This is the method that actually does the work.
        /// </summary>
        /// <param name="DA">The DA object is used to retrieve from inputs and store in outputs.</param>
        protected override void SolveInstance(IGH_DataAccess DA)
        {
            // Degine placeholder variables
            string macroName = "Drawing symbol 2";
            string viewProperty = string.Empty;
            TSD.View containView = null;

            // Load values from inputs into those variables
          
            if (!DA.GetData(0, ref viewProperty)) return;
            if (!DA.GetData(1, ref containView)) return;

            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            dh.GetDrawingObjectSelector().SelectObject(containView);
           
   
            string macroPath = string.Empty;
            string proPath = string.Empty;

            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroPath);
            if (macroPath.IndexOf(';') > 0) { macroPath = macroPath.Remove(macroPath.IndexOf(";")); };

            //Create a script as string
            string macros =
                @"#pragma warning disable 1633 // Unrecognized #pragma directive" + "\r\n" +
                @"#pragma reference ""Tekla.Macros.Akit""" + "\r\n" +
                @"#pragma reference ""Tekla.Macros.Runtime""" + "\r\n" +
                @"#pragma warning restore 1633 // Unrecognized #pragma directive" + "\r\n" +

                @"namespace UserMacros {" + "\r\n" +
                    @"public sealed class Macro {" + "\r\n" +

                        @"[Tekla.Macros.Runtime.MacroEntryPointAttribute()]" + "\r\n" +
                        @"public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {" + "\r\n" +
                            @"Tekla.Macros.Akit.IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();" + "\r\n" +
                            @"akit.Callback(""acmd_display_selected_drawing_object_dialog"", """", ""View_10 window_1"");" + "\r\n" +
                            @"akit.ValueChange(""view_dial"", ""gr_view_get_menu"", "+"\""+ viewProperty +"\""+");" + "\r\n" +
                            @"akit.PushButton(""gr_view_get"", ""view_dial"");" + "\r\n" +
                            @"akit.PushButton(""view_modify"", ""view_dial"");" + "\r\n" +
                            @"akit.PushButton(""view_ok"", ""view_dial"");" + "\r\n" +
                        @"}" + "\r\n" +
                    @"}" + "\r\n" +
                @"}";

            string drawingPath = @"\drawings";
            File.WriteAllText(Path.Combine(macroPath + drawingPath, macroName), macros);
            string macroPathDwg = @"..\drawings\";

            bool result = TSMO.Operation.RunMacro(macroPathDwg + macroName);

            dh.GetDrawingObjectSelector().UnselectAllObjects();
            dr.CommitChanges();
            //if (result)
            //{
            //    MessageBox.Show("Macro ran successfully!");
            //}
            //else
            //{
            //    MessageBox.Show("Failed to run macro.");
            //}
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.view_property;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("BE302780-32E0-4A7C-B591-550A8BE59218"); }
        }
    }
}