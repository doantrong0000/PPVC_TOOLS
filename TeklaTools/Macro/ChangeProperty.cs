#pragma warning disable 1633
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633

namespace UserMacros
{
    public sealed class Macro
    {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime)
        {
            Tekla.Macros.Akit.IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();
            Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf = runtime.Get<Tekla.Macros.Wpf.Runtime.IWpfMacroHost>();
            wpf.InvokeCommand("CommandRepository", "Dimensions.AddRebarDimensionMark");
            wpf.InvokeCommand("CommandRepository", "Dimensions.AddRebarDimensionMark");
            akit.ValueChange("rebar_dim_dial", "gr_dim_get_menu", "<PROPERTY_NAME>");
            akit.PushButton("gr_dim_get", "rebar_dim_dial");
            akit.PushButton("dim_apply", "rebar_dim_dial");
            akit.PushButton("dim_ok", "rebar_dim_dial");
            akit.CommandEnd();
        }
    }
}