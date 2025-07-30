using System;
using System.Collections.Generic;

using static Celeste.Input;

namespace Celeste.Mod.FancyTextExtended;

public static class InputExt
{
    public static readonly Dictionary<string, Func<VirtualInput>> RegisteredInputNames = new()
    {
        { "ESC", () => ESC },
        { "Pause", () => Pause },
        { "MenuLeft", () => MenuLeft },
        { "MenuRight", () => MenuRight },
        { "MenuUp", () => MenuUp },
        { "MenuDown", () => MenuDown },
        { "MenuConfirm", () => MenuConfirm },
        { "MenuCancel", () => MenuCancel },
        { "MenuJournal", () => MenuJournal },
        { "QuickRestart", () => QuickRestart },
        { "MoveX", () => MoveX },
        { "MoveY", () => MoveY },
        { "GliderMoveY", () => GliderMoveY },
        { "Aim", () => Aim },
        { "Feather", () => Feather },
        { "MountainAim", () => MountainAim },
        { "Jump", () => Jump },
        { "Dash", () => Dash },
        { "Grab", () => Grab },
        { "Talk", () => Talk },
        { "CrouchDash", () => CrouchDash }
    };

    public static void RegisterInputName(string name, Func<VirtualInput> inputGetter)
        => RegisteredInputNames[name] = inputGetter;

    public static VirtualInput? GetRegisteredInput(string name)
        => RegisteredInputNames.TryGetValue(name, out var inputGetter) ? inputGetter() : null;

    public static Func<VirtualInput>? GetRegisteredInputGetter(string name)
        => RegisteredInputNames.TryGetValue(name, out var inputGetter) ? inputGetter : null;

    public static Binding BindingOrAlt(Binding binding, Binding? bindingAlt)
    {
        return (bindingAlt is null || binding.HasInput) ? binding : bindingAlt;
    }
}
