#pragma warning disable 1633 // Unrecognized #pragma directive
#pragma reference "Tekla.Macros.Wpf.Runtime"
#pragma reference "Tekla.Macros.Akit"
#pragma reference "Tekla.Macros.Runtime"
#pragma warning restore 1633 // Unrecognized #pragma directive

namespace UserMacros {
    public sealed class Macro {
        [Tekla.Macros.Runtime.MacroEntryPointAttribute()]
        public static void Run(Tekla.Macros.Runtime.IMacroRuntime runtime) {
            Tekla.Macros.Akit.IAkitScriptHost akit = runtime.Get<Tekla.Macros.Akit.IAkitScriptHost>();
            Tekla.Macros.Wpf.Runtime.IWpfMacroHost wpf = runtime.Get<Tekla.Macros.Wpf.Runtime.IWpfMacroHost>();
            wpf.InvokeCommand("CommandRepository", "Annotations.AddPartMarksForSelected");
            wpf.InvokeCommand("CommandRepository", "Annotations.AddPartMarksForSelected");
            akit.ValueChange("note_dial", "gr_note_get_menu", "WH_12mm Wireloop");
            akit.PushButton("gr_vpm_get", "note_dial");
            akit.PushButton("vpm_apply", "note_dial");
            akit.PushButton("vpm_apply", "note_dial");
            akit.PushButton("vpm_apply", "note_dial");
            akit.PushButton("vpm_apply", "note_dial");
        }
    }
}
