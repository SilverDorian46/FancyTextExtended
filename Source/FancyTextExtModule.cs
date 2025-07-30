global using Celeste.Mod.Entities;
global using Microsoft.Xna.Framework;
global using Monocle;
global using Celeste.Mod.FancyTextExtended;

using System;

using Celeste.Mod.FancyTextExtended.Hooks;

namespace Celeste.Mod.FancyTextExtended;

public class FancyTextExtModule : EverestModule
{
    public static FancyTextExtModule Instance { get; private set; } = null!;

    public FancyTextExtModule()
    {
        Instance = this;
#if DEBUG
        // debug builds use verbose logging
        Logger.SetLogLevel(nameof(FancyTextExtModule), LogLevel.Verbose);
#else
        // release builds use info logging to reduce spam in log files
        Logger.SetLogLevel(nameof(FancyTextExtendedModule), LogLevel.Info);
#endif
    }

    public override void Load()
    {
        FancyTextHooks.Load();
        TextboxHooks.Load();
        MiniTextboxHooks.Load();
    }

    public override void Unload()
    {
        FancyTextHooks.Unload();
        TextboxHooks.Unload();
        MiniTextboxHooks.Unload();
    }
}