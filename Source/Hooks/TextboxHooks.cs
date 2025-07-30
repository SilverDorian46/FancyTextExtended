using System;
using System.Reflection;
using System.Xml;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

using static Celeste.Textbox;

namespace Celeste.Mod.FancyTextExtended.Hooks;

internal static class TextboxHooks
{
    private static readonly MethodInfo m_RunRoutine_MoveNext = typeof(Textbox)
        .GetMethod(nameof(Textbox.RunRoutine), BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetStateMachineTarget()!;
    private static readonly MethodInfo m_EaseOpen_MoveNext = typeof(Textbox)
        .GetMethod(nameof(Textbox.EaseOpen), BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetStateMachineTarget()!;
    private static readonly MethodInfo m_EaseClose_MoveNext = typeof(Textbox)
        .GetMethod(nameof(Textbox.EaseClose), BindingFlags.NonPublic | BindingFlags.Instance)!
        .GetStateMachineTarget()!;

    private static ILHook? hook_RunRoutine;
    private static ILHook? hook_EaseOpen;
    private static ILHook? hook_EaseClose;

    internal static void Load()
    {
        hook_RunRoutine = new ILHook(m_RunRoutine_MoveNext, IL_RunRoutine_MoveNext);
        hook_EaseOpen = new ILHook(m_EaseOpen_MoveNext, IL_EaseOpen_MoveNext);
        hook_EaseClose = new ILHook(m_EaseClose_MoveNext, IL_EaseClose_MoveNext);
    }

    internal static void Unload()
    {
        hook_RunRoutine?.Dispose(); hook_RunRoutine = null;
        hook_EaseOpen?.Dispose(); hook_EaseOpen = null;
        hook_EaseClose?.Dispose(); hook_EaseClose = null;
    }

    private static void IL_RunRoutine_MoveNext(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.RunRoutine");

        /*
         IEnumerator code:
         
         while (index < Nodes.Count)
         {
             FancyText.Node current = Nodes[index];
             float delay = 0;
              <-- if (CheckFTXNode(this, current)) { }
              <-- else
             if (current is FancyText.Anchor) // if-else chain begins here
             [...]
             else if (current is FancyText.Portrait)
             {
                 [...]
                 string text2 = xmlElement.Attr("phonestatic", "");
                 if (!string.IsNullOrEmpty(text2)) { [...] }
                  <-- CheckFTXPhonestaticEventAttr(this, xmlElement);
                 canSkip = false;
                 [...]
             }
             [...]
             else if (current is FancyText.Char)
             {
                 var ch = current as FancyText.Char;
                 [...]
                 // after EaseOpen
                 bool flag = false;
                 [...]
                      ModFlagIfFTXNoTalk(    , textbox, ch)
                      vvvvvvvvvvvvvvvvvvv    vvvvvvvvvvvvvv
                 if (!                   flag               && !ch.IsPunctuation)
                     PlayTalkAnimation();
                 [...]
             }
             
             // end of if-else chain
             last = current;
             index++;
             [...]
         }
         */

        /*
         MoveNext code:
         
         int state = <>1__state;
         [...]
         switch (state)
         { [...]
         case 15:
             // current portrait after EaseClose label
             IL_01ac:
             [...]
             string text2 = xmlElement.Attr("phonestatic", "");
             if (!string.IsNullOrEmpty(text2)) { [...] }
              <-- CheckFTXPhonestaticEventAttr(textbox, xmlElement);
             textbox.canSkip = false;
             [...]
             
             // current char after EaseOpen label
             IL_07bf:
             flag = false;
             [...]
                  ModFlagIfFTXNoTalk(    , textbox, <ch>5__9)
                  vvvvvvvvvvvvvvvvvvv    vvvvvvvvvvvvvvvvvvvv
             if (!                   flag                     && !<ch>5__9.IsPunctuation)
                 textbox.PlayTalkAnimation();
             [...]
             
             // increment index label
             IL_08a3:
             <last>5__2 = <current>5__4;
			 textbox.index++;
             [...]
             
             // current index label
             IL_0926:
             if (textbox.index < textbox.Nodes.Count)
               // this actually branches to IL_0074 in IL, so now we have to move backwards.
             {
                 // IL_0074
                 <current>5__4 = textbox.Nodes[textbox.index];
				 <delay>5__5 = 0;
                  <-- if (CheckFTXNode(textbox, <current>5__4)) { }
                  <-- else
                 if (<current>5__4 is FancyText.Anchor) // if-else chain begins here
                 [...]
             }
             goto IL_08a3; // go to increment index label
             [...]
             
             // end of case
         }
         */

        // fields
        Type declType = m_RunRoutine_MoveNext.DeclaringType!;
        FieldInfo f_current = declType.GetField("<current>5__4", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo f_delay = declType.GetField("<delay>5__5", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo f_ch = declType.GetField("<ch>5__9", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // starting from IL_0074
        ILLabel? incrementIndexLabel = default!;

        cur.GotoNext(instr => instr.MatchIsinst<FancyText.Anchor>());

        // get label IL_08a3 from `if (textbox.RenderOffset == Vector2.Zero)`
        cur.Clone()
            .GotoNext(
                instr => instr.MatchCall<Vector2>("get_" + nameof(Vector2.Zero)),
                instr => instr.MatchCall<Vector2>("op_Equality")
            ).GotoNext(instr => instr.MatchBrfalse(out incrementIndexLabel));

        cur.GotoPrev(MoveType.After, instr => instr.MatchStfld(f_delay))
            .MoveAfterLabels();

        cur.EmitLdloc1(); // textbox
        cur.EmitLdarg0(); // this... (the IEnumerator object)
        cur.EmitLdfld(f_current); // this.<current>5__4
        cur.EmitDelegate(CheckFTXNode); // CheckFTXNode(textbox, <current>5__4)
        cur.EmitBrtrue(incrementIndexLabel); // if true, go to that label

        // moving on through IL_01ac
        ILLabel? afterPhonestaticLabel = default!;

        cur.GotoNext(instr => instr.MatchLdstr("phonestatic"))
            .GotoNext(instr => instr.MatchBrtrue(out afterPhonestaticLabel))
            .Goto(afterPhonestaticLabel.Target, MoveType.AfterLabel);

        cur.EmitLdloc1(); // textbox
        cur.EmitLdloc2(); // xmlElement
        cur.EmitDelegate(CheckFTXPhonestaticEventAttr); // CheckFTXPhonestaticEventAttr(textbox, xmlElement)

        // moving on through IL_07bf
        cur.GotoNext(instr => instr.MatchLdfld<FancyText.Char>(nameof(FancyText.Char.IsPunctuation)))
            .GotoPrev(instr => instr.MatchBrtrue(out _))
            .GotoPrev(MoveType.After, instr => instr.MatchLdloc(9)); // flag

        cur.EmitLdloc1(); // textbox
        cur.EmitLdarg0(); // this
        cur.EmitLdfld(f_ch); // this.<ch>5__9
        cur.EmitDelegate(ModFlagIfFTXNoTalk); // ModFlagIfFTXNoTalk(flag, textbox, <ch>5__9)
    }

    // leftover :shrug:
    private static bool CheckFTXNode(Textbox textbox, FancyText.Node current)
    {
        return false;
    }

    private static void CheckFTXPhonestaticEventAttr(Textbox textbox, XmlElement xml)
    {
        string? phonestaticEvent = xml.Attr(PortraitSpriteBankExt.PhonestaticEventAttr, defaultValue: null);
        if (!string.IsNullOrEmpty(phonestaticEvent))
            textbox.phonestatic.Play(phonestaticEvent);
    }

    private static bool ModFlagIfFTXNoTalk(bool origFlag, Textbox textbox, FancyText.Char ch)
    {
        if (DynamicData.For(ch).TryGet(FancyTextExt.Char_NoTalkField, out bool? noTalk) && noTalk == true)
        {
            textbox.PlayIdleAnimation();
            return true;
        }

        return origFlag;
    }

    private static void IL_EaseOpen_MoveNext(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.EaseOpen");

        /*
         IEnumerator code:
         
         if (portrait != null)
         {
              <-- if (TryPlayFTXEaseOpenSound(this)) goto afterSoundLabel;
             if (portrait.Sprite.IndexOf("madeline", [...]) >= 0)
                 Audio.Play("event:/ui/game/textbox_madeline_in");
         }
         else
             Audio.Play("event:/ui/game/textbox_other_in");
          <-- afterSoundLabel:
         */

        /*
         MoveNext code:
         
         if (textbox.portrait != null)
         {
              <-- if (TryPlayFTXEaseOpenSound(textbox)) goto afterSoundLabel;
             if (textbox.portrait.Sprite.IndexOf("madeline", [...]) >= 0)
                 Audio.Play("event:/ui/game/textbox_madeline_in");
         }
         else
             Audio.Play("event:/ui/game/textbox_other_in");
          <-- afterSoundLabel:
         */

        ILLabel? afterSoundLabel = default!;

        cur.GotoNext(instr => instr.MatchLdfld<Textbox>(nameof(Textbox.portrait)))
            .GotoNext(MoveType.After, instr => instr.MatchBrfalse(out _))
            .MoveAfterLabels();

        cur.Clone()
            .GotoNext(instr => instr.MatchCall("Celeste.Audio", nameof(Audio.Play)))
            .GotoNext(instr => instr.MatchBr(out afterSoundLabel));

        cur.EmitLdloc1(); // textbox
        cur.EmitDelegate(TryPlayFTXEaseOpenSound); // TryPlayFTXEaseOpenSound(textbox)
        cur.EmitBrtrue(afterSoundLabel); // go to label if true
    }

    private static bool TryPlayFTXEaseOpenSound(Textbox textbox)
    {
        if (GFX.PortraitsSpriteBank.SpriteData.TryGetValue(textbox.portrait.SpriteId, out var spriteData))
        {
            XmlElement xml = spriteData.Sources[0].XML;
            string? easeSfx = xml.Attr(PortraitSpriteBankExt.EaseSfxAttr, defaultValue: null);
            if (!string.IsNullOrEmpty(easeSfx))
            {
                Audio.Play(easeSfx + "_in");
                return true;
            }
        }

        return false;
    }

    private static void IL_EaseClose_MoveNext(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.EaseClose");

        /*
         IEnumerator code:
         
         if (portrait != null)
         {
              <-- if (TryPlayFTXEaseCloseSound(this)) goto afterSoundLabel;
             if (portrait.Sprite.IndexOf("madeline", [...]) >= 0)
                 Audio.Play("event:/ui/game/textbox_madeline_out");
         }
         else
             Audio.Play("event:/ui/game/textbox_other_out");
          <-- afterSoundLabel:
         */

        /*
         MoveNext code:
         
         if (textbox.portrait != null)
         {
              <-- if (TryPlayFTXEaseCloseSound(textbox)) goto afterSoundLabel;
             if (textbox.portrait.Sprite.IndexOf("madeline", [...]) >= 0)
                 Audio.Play("event:/ui/game/textbox_madeline_out");
         }
         else
             Audio.Play("event:/ui/game/textbox_other_out");
          <-- afterSoundLabel:
         */

        ILLabel? afterSoundLabel = default!;

        cur.GotoNext(instr => instr.MatchLdfld<Textbox>(nameof(Textbox.portrait)))
            .GotoNext(MoveType.After, instr => instr.MatchBrfalse(out _))
            .MoveAfterLabels();

        cur.Clone()
            .GotoNext(instr => instr.MatchCall("Celeste.Audio", nameof(Audio.Play)))
            .GotoNext(instr => instr.MatchBr(out afterSoundLabel));

        cur.EmitLdloc1(); // textbox
        cur.EmitDelegate(TryPlayFTXEaseCloseSound); // TryPlayFTXEaseCloseSound(textbox)
        cur.EmitBrtrue(afterSoundLabel); // go to label if true
    }

    private static bool TryPlayFTXEaseCloseSound(Textbox textbox)
    {
        if (GFX.PortraitsSpriteBank.SpriteData.TryGetValue(textbox.portrait.SpriteId, out var spriteData))
        {
            XmlElement xml = spriteData.Sources[0].XML;
            string? easeSfx = xml.Attr(PortraitSpriteBankExt.EaseSfxAttr, defaultValue: null);
            if (!string.IsNullOrEmpty(easeSfx))
            {
                Audio.Play(easeSfx + "_out");
                return true;
            }
        }

        return false;
    }
}
