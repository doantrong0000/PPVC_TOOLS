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
    public class AddRebarDim : GH_Component
    {
        /// <summary>
        /// Initializes a new instance of the MyComponent1 class.
        /// </summary>
        public AddRebarDim()
          : base("Add Tekla rebar dim", "Apply rebar dim",
              "Apply rebar dim",
              "Tekla Drawing 2020", "Properties")
        {
        }

        /// <summary>
        /// Registers all the input parameters for this component.
        /// </summary>
        protected override void RegisterInputParams(GH_Component.GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Name", "name", "add a name", GH_ParamAccess.item);
          

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
            string Name = string.Empty;       
            string macroName = "Drawing symboll";       
            

            // Load values from inputs into those variables
            if (!DA.GetData(0, ref Name)) return;
           


            //Connect with drawing environment
            TSD.DrawingHandler dh = new TSD.DrawingHandler();
            // Working with active drawing
            TSD.Drawing dr = dh.GetActiveDrawing();

            

            string macroPath = string.Empty;
            string proPath = string.Empty;

            Tekla.Structures.TeklaStructuresSettings.GetAdvancedOption("XS_MACRO_DIRECTORY", ref macroPath);
            if (macroPath.IndexOf(';') > 0) { macroPath = macroPath.Remove(macroPath.IndexOf(";")); };

            //Create a script as string
            string macros =
                @"#pragma warning disable 1633 // Unrecognized #pragma directive" + "\r\n" +
                @"#pragma reference ""Tekla.Macros.Wpf.Runtime""" + "\r\n" +
                @"#pragma reference ""Tekla.Macros.Akit""" + "\r\n" +
                @"#pragma reference ""Tekla.Macros.Runtime""" + "\r\n" +
                @"#pragma warning restore 1633 // Unrecognized #pragma directive" + "\r\n" +

                @"namespace UserMacros {" + "\r\n" +
                    @"public sealed class Macro {" + "\r\n" +

                        @"[Tekla.Macros.Runtime.MacroEntryPointAttribute()]" + "\r\n" +
                        @"public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {" + "\r\n" +
                            @"Tekla.Macros.Akit.IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();" + "\r\n" +

                            @"Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf = runtime.Get<Tekla.Macros.Wpf.Runtime.IWpfMacroHost>();" + "\r\n" +
                            
                            @"wpf.InvokeCommand(""CommandRepository"", ""Dimensions.AddRebarDimensionMark"");" + "\r\n" +

                        @"}" + "\r\n" +
                    @"}" + "\r\n" +
                @"}";

            string drawingPath = @"\drawings";
            File.WriteAllText(Path.Combine(macroPath + drawingPath, macroName), macros);
            string macroPathDwg = @"..\drawings\";

            bool result = TSMO.Operation.RunMacro(macroPathDwg + macroName);

       
        }

        /// <summary>
        /// Provides an Icon for the component.
        /// </summary>
        protected override System.Drawing.Bitmap Icon => Properties.Resources.add_rebar_dim;

        /// <summary>
        /// Gets the unique ID for this component. Do not change this ID after release.
        /// </summary>
        public override Guid ComponentGuid
        {
            get { return new Guid("BE302780-3230-4A7C-B591-550A8BE59238"); }
        }
    }
}