using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

using static Celeste.MiniTextbox;

namespace Celeste.Mod.FancyTextExtended.Hooks;

internal static class MiniTextboxHooks
{
    private static readonly MethodInfo m_Routine_MoveNext = typeof(MiniTextbox)
        .GetMethod(nameof(MiniTextbox.Routine), BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetStateMachineTarget()!;

    private static ILHook? hook_Routine;

    internal static void Load()
    {
        hook_Routine = new ILHook(m_Routine_MoveNext, IL_Routine_MoveNext);
    }

    internal static void Unload()
    {
        hook_Routine?.Dispose(); hook_Routine = null;
    }

    private static void IL_Routine_MoveNext(ILContext il)
    {
        ILCursor cur = new(il);

        /*
         IEnumerator code:
         
         while (index < text.Nodes.Count)
         {
             [...]
             if (text.Nodes[index] is FancyText.Char)
             {
                          CheckFTXNoTalk(                                   , this)
                          vvvvvvvvvvvvvvv                                   vvvvvvv
                 delay +=                text.Nodes[index] as FancyText.Char       .Delay;
             }
             [...]
         }
         */

        /*
         MoveNext code:
         
         MiniTextbox mini = <>4__this;
         [...]
         
         while (mini.index < mini.text.Nodes.Count)
         {
             [...]
             if (mini.text.Nodes[mini.index] is FancyText.Char)
             {
                          CheckFTXNoTalk(                                             , mini)
                          vvvvvvvvvvvvvvv                                             vvvvvvv
                 delay +=                mini.text.Nodes[mini.index] as FancyText.Char       .Delay;
             }
             [...]
         }
         */

        cur.GotoNext(MoveType.Before, instr => instr.MatchLdfld<FancyText.Char>(nameof(FancyText.Char.Delay)));
        // after (mini.text.Nodes[mini.index] as FancyText.Char)

        cur.EmitLdloc1(); // mini
        cur.EmitDelegate(CheckFTXNoTalk); // CheckFTXNoTalk(mini.text.Nodes[mini.index] as FancyText.Char, mini)
        // returns the same FancyText.Char
    }

    private static FancyText.Char CheckFTXNoTalk(FancyText.Char ch, MiniTextbox mini)
    {
        if (DynamicData.For(ch).TryGet(FancyTextExt.Char_NoTalkField, out bool? noTalk) && noTalk == true)
            StopTalking(mini);

        return ch;
    }

    private static void StopTalking(MiniTextbox mini)
    {
        if (mini.talkerSfx != null)
        {
            mini.talkerSfx.Param("dialogue_portrait", 0);
            mini.talkerSfx.Param("dialogue_end", 1);
        }
        if (mini.portrait != null && mini.portraitData != null
            && mini.portrait.Has(mini.portraitData.IdleAnimation)
            && mini.portrait.CurrentAnimationID != mini.portraitData.IdleAnimation)
        {
            mini.portrait.Play(mini.portraitData.IdleAnimation);
        }
    }
}
