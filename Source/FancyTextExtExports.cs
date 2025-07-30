using MonoMod.ModInterop;
using System;

namespace Celeste.Mod.FancyTextExtended;

[ModExportName("FancyTextExtended")]
public static class FancyTextExtExports
{
    public static void RegisterInputName(string name, Func<VirtualInput> inputGetter)
        => InputExt.RegisterInputName(name, inputGetter);
}
