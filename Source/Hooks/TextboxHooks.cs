using System;
using System.Collections;
using System.Reflection;
using System.Xml;
using Mono.Cecil.Cil;
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

    /// <summary>field for <see cref="Textbox.RunRoutine"/> of type <see cref="IEnumerator"/></summary>
    private const string RunRoutine_AwaitEnumField = "FancyTextExt:awaitEnum";

    internal static void Load()
    {
        hook_RunRoutine = new ILHook(m_RunRoutine_MoveNext, IL_RunRoutine_MoveNext);
        hook_EaseOpen = new ILHook(m_EaseOpen_MoveNext, IL_EaseOpen_MoveNext);
        hook_EaseClose = new ILHook(m_EaseClose_MoveNext, IL_EaseClose_MoveNext);

        IL.Celeste.Textbox.Update += IL_Update;
        IL.Celeste.Textbox.Render += IL_Render;
    }

    internal static void Unload()
    {
        hook_RunRoutine?.Dispose(); hook_RunRoutine = null;
        hook_EaseOpen?.Dispose(); hook_EaseOpen = null;
        hook_EaseClose?.Dispose(); hook_EaseClose = null;

        IL.Celeste.Textbox.Update -= IL_Update;
        IL.Celeste.Textbox.Render -= IL_Render;
    }

    private static void IL_RunRoutine_MoveNext(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.RunRoutine");

        /*
         virtual IEnumerator code:
         
         while (index < Nodes.Count)
         {
             FancyText.Node current = Nodes[index];
             float delay = 0;
              <-- if (CheckFTXNode(this, current)) { }
              <-- else if (CheckIfFTXAwaitNode(this, current))
              <-- {
              <--     IEnumerator awaitEnum = WaitForInputMidPage(this);
              <--     while (awaitEnum.MoveNext())
              <--         yield return awaitEnum.Current;
              <-- }
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
         
          <-- DynamicData runRoutineData = GetRunRoutineDynamicData(this);
         int state = <>1__state;
         [...]
         switch (state)
         {
         default:
              <-- if (CheckAwaitRoutine(runRoutineData, out IEnumerator awaitEnum))
              <-- {
              <--     if (DoAwaitRoutine(awaitEnum, ref <>2__current))
              <--         return true;
              <--     
              <--     goto IL_08a3; // go to increment index label
              <-- }
             return false;
         [...]
         
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
                  <-- else if (CheckIfFTXAwaitNode(textbox, <current>5__4))
                  <-- {
                  <--     if (TryStartAwaitRoutine(runRoutineData, textbox, ref <>2__current))
                  <--         return true;
                  <-- }
                  <-- else
                 if (<current>5__4 is FancyText.Anchor) // if-else chain begins here
                 [...]
                 
                 // end of if-else chain
                 goto IL_08a3; // go to increment index label
             }
             [...]
             
             // end of case
         }
         */

        // fields
        Type declType = m_RunRoutine_MoveNext.DeclaringType!;
        FieldInfo f_smCurrent = declType.GetField("<>2__current", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo f_current = declType.GetField("<current>5__4", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo f_delay = declType.GetField("<delay>5__5", BindingFlags.NonPublic | BindingFlags.Instance)!;
        FieldInfo f_ch = declType.GetField("<ch>5__9", BindingFlags.NonPublic | BindingFlags.Instance)!;

        // new variables
        VariableDefinition v_runRoutineData = new(il.Import(typeof(DynamicData)));
        il.Body.Variables.Add(v_runRoutineData);

        VariableDefinition v_awaitEnumToContinue = new(il.Import(typeof(IEnumerator)));
        il.Body.Variables.Add(v_awaitEnumToContinue);

        // at the beginning
        cur.EmitLdarg0(); // this (the IEnumerator object)
        cur.EmitDelegate(GetRunRoutineDynamicData); // GetRunRoutineDynamicData(this)
        cur.EmitStloc(v_runRoutineData); // DynamicData runRoutineData = ^^^;

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

        ILLabel nextIfElseLabel = cur.DefineLabel();

        cur.EmitLdloc1(); // textbox
        cur.EmitLdarg0(); // this...
        cur.EmitLdfld(f_current); // this.<current>5__4
        cur.EmitDelegate(CheckIfFTXAwaitNode); // CheckIfFTXAwaitNode(textbox, <current>5__4)
        cur.EmitBrfalse(nextIfElseLabel); // if false, move on to the next check in the if-else chain

        cur.EmitLdloc(v_runRoutineData); // runRoutineData
        cur.EmitLdloc1(); // textbox
        cur.EmitLdarg0(); // this...
        cur.EmitLdflda(f_smCurrent); // ref this.<>2__current
        cur.EmitDelegate(TryStartAwaitRoutine); // TryStartAwaitRoutine(runRoutineData, textbox, ref <>2__current)
        cur.EmitBrfalse(incrementIndexLabel); // if false, go to that label

        cur.EmitLdcI4(1); // true
        cur.EmitRet(); // return true;

        cur.MarkLabel(nextIfElseLabel);

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

        // now back to the beginning
        cur.Index = 0;

        // into the default label of the switch block
        // will be accessed if the state is -1 and the state machine has returned true without changing the state
        // as is the case for our injected instructions which make the state machine return true
        cur.GotoNext(instr => instr.MatchSwitch(out _))
            .GotoNext(
                MoveType.AfterLabel,
                instr => instr.MatchLdcI4(0),
                instr => instr.MatchRet()
            );

        ILLabel origDefaultCaseLabel = cur.DefineLabel();

        cur.EmitLdloc(v_runRoutineData); // runRoutineData
        cur.EmitLdloca(v_awaitEnumToContinue); // out awaitEnumToContinue
        cur.EmitDelegate(CheckAwaitRoutine); // CheckAwaitRoutine(runRoutineData, out IEnumerator awaitEnumToContinue)
        cur.EmitBrfalse(origDefaultCaseLabel); // if false, execute the default case
                                               // (will just make the state machine return false)

        cur.EmitLdloc(v_awaitEnumToContinue); // awaitEnumToContinue
        cur.EmitLdarg0(); // this...
        cur.EmitLdflda(f_smCurrent); // ref this.<>2__current
        cur.EmitDelegate(DoAwaitRoutine); // DoAwaitRoutine(awaitEnumToContinue, ref <>2__current)
        cur.EmitBrfalse(incrementIndexLabel); // if false, go to that label

        cur.EmitLdcI4(1); // true
        cur.EmitRet(); // return true;

        cur.MarkLabel(origDefaultCaseLabel);
    }

    private static DynamicData GetRunRoutineDynamicData(IEnumerator runRoutine)
        => DynamicData.For(runRoutine);

    // if true, increment index and move on to the next node
    // if false, check for base game nodes (or special case nodes if any)
    private static bool CheckFTXNode(Textbox textbox, FancyText.Node current)
    {
        return false;
    }

    // if true, call TryStartAwaitRoutine below (starts awaitEnum)
    // if false, check for base game nodes (or any other special case nodes)
    private static bool CheckIfFTXAwaitNode(Textbox textbox, FancyText.Node current)
    {
        if (current is FancyTextExt.AwaitNode awaitNode)
        {
            awaitNode.IsVisible = () => textbox.GetCurrentNode() == awaitNode && textbox.waitingForInput;
            return true;
        }

        return false;
    }

    // if true, make the state machine return true
    // if false, increment index and move on to the next node
    private static bool TryStartAwaitRoutine(DynamicData runRoutineData, Textbox textbox, ref object _current)
    {
        IEnumerator awaitEnum = WaitForInputMidPage(runRoutineData, textbox);
        runRoutineData.Set(RunRoutine_AwaitEnumField, awaitEnum);
        
        return DoAwaitRoutine(awaitEnum, ref _current);
    }

    // if true, call DoAwaitRoutine below (continues awaitEnum)
    // if false, proceed with the original execution
    private static bool CheckAwaitRoutine(DynamicData runRoutineData, out IEnumerator? awaitEnum)
        => runRoutineData.TryGet(RunRoutine_AwaitEnumField, out awaitEnum) && awaitEnum is not null;

    // if true, make the state machine return true
    // if false, increment index and move on to the next node
    private static bool DoAwaitRoutine(IEnumerator awaitEnum, ref object _current)
    {
        if (awaitEnum.MoveNext())
        {
            _current = awaitEnum.Current;
            return true;
        }

        return false;
    }

    private static IEnumerator WaitForInputMidPage(DynamicData runRoutineData, Textbox textbox)
    {
        textbox.PlayIdleAnimation();
        if (textbox.ease >= 1)
        {
            textbox.waitingForInput = true;
            yield return 0.1f;

            while (!textbox.ContinuePressed())
                yield return null;

            textbox.waitingForInput = false;
        }

        runRoutineData.Set(RunRoutine_AwaitEnumField, null);
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
         virtual IEnumerator code:
         
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

    private static void IL_Update(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.Update");

        /*
         int currentIndex = Math.Min(index, Nodes.Count);
          <-- CheckCurrentNodeOnUpdate(this, currentIndex);
		 for (int i = Start; i < currentIndex; i++) { [...] }
         */

        cur.GotoNext(MoveType.After, instr => instr.MatchStloc1());

        cur.EmitLdarg0(); // this
        cur.EmitLdloc1(); // currentIndex
        cur.EmitDelegate(CheckCurrentNodeOnUpdate); // CheckNodeOnUpdate(this, currentIndex)
        

        Logger.Info(nameof(FancyTextExtended), il.ToString());
    }

    private static void CheckCurrentNodeOnUpdate(Textbox textbox, int currentIndex)
    {
        if (textbox.GetCurrentNode() is FancyTextExt.AwaitNode awaitNode && textbox.waitingForInput)
            awaitNode.Timer = textbox.timer;
    }

    private static void IL_Render(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for Textbox.Render");

        /*
             NotWaitingAtAwaitNode(               , this)
             vvvvvvvvvvvvvvvvvvvvvv               vvvvvvv
         if (                      waitingForInput       )
         {
             [...]
             GFX.Gui["textboxbutton"].DrawCentered(position);
         }
         */

        // go to waitingForInput
        cur.GotoNext(MoveType.After, instr => instr.MatchLdfld<Textbox>(nameof(Textbox.waitingForInput)));

        cur.EmitLdarg0(); // this
        cur.EmitDelegate(NotWaitingAtAwaitNode); // NotWaitingAtAwaitNode(waitingForInput, this)


        Logger.Info(nameof(FancyTextExtended), il.ToString());
    }

    private static bool NotWaitingAtAwaitNode(bool waitingForInput, Textbox textbox)
        => waitingForInput && textbox.GetCurrentNode() is not FancyTextExt.AwaitNode;
}
