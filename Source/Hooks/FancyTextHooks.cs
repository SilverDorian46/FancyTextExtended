using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

using static Celeste.FancyText;
using static Celeste.Mod.FancyTextExtended.FancyTextExt;

namespace Celeste.Mod.FancyTextExtended.Hooks;

internal static class FancyTextHooks
{
    private readonly static MethodInfo m_orig_AddWord = typeof(FancyText)
        .GetMethod("orig_" + nameof(FancyText.AddWord), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static ILHook? hook_orig_AddWord;

    internal static void Load()
    {
        IL.Celeste.FancyText.Parse += IL_Parse;

        hook_orig_AddWord = new(m_orig_AddWord, IL_orig_AddWord);

        On.Celeste.FancyText.Text.Draw += On_Text_Draw;
        On.Celeste.FancyText.Text.DrawJustifyPerLine += On_Text_DrawJustifyPerLine;
    }

    internal static void Unload()
    {
        IL.Celeste.FancyText.Parse -= IL_Parse;

        hook_orig_AddWord?.Dispose(); hook_orig_AddWord = null;

        On.Celeste.FancyText.Text.Draw -= On_Text_Draw;
        On.Celeste.FancyText.Text.DrawJustifyPerLine -= On_Text_DrawJustifyPerLine;
    }

    private static void IL_Parse(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for FancyText.Parse");

        /*
         j++;
         string cmd = array2[j++];
         List<string> paramList = new List<string>();
         
         [...]
         
         if ([...]) { [...] continue; }
          <-- if (ParseFTXCommand(this, cmd, paramList)) continue;
         if (!cmd.Equals("savedata")) continue;
         [...]
         */

        ILLabel? continueLabel = default!;

        cur.GotoNext(instr => instr.MatchLdstr("savedata"))
            .GotoPrev(MoveType.After, instr => instr.MatchBr(out continueLabel))
            .MoveAfterLabels();

        cur.EmitLdarg0(); // this
        cur.EmitLdloc(7); // cmd
        cur.EmitLdloc(8); // paramList
        cur.EmitDelegate(ParseFTXCommand); // ParseFTXCommand(this, cmd, paramList)
        cur.EmitBrtrue(continueLabel); // if true, go to continue label
    }

    private static bool ParseFTXCommand(FancyText fText, string cmd, List<string> paramList)
    {
        var fTextData = DynamicData.For(fText);

        switch (cmd)
        {
            case NoTalkCmd:
                fTextData.Set(CurrentNoTalkField, true);
                return true;

            case EndNoTalkCmd:
                fTextData.Set(CurrentNoTalkField, false);
                return true;

            case InputCmd:
                if (TryAddInputDirection(fText, paramList)) { }
                else if (TryAddInputIcon(fText, paramList)) { }
                else fText.AddWord("\0");
                return true;
        }

        return false;
    }

    private static bool TryAddInputIcon(FancyText fText, List<string> paramList)
    {
        if (paramList.Count <= 0)
            return false;

        string name = paramList[0];
        if (InputExt.GetRegisteredInputGetter(name) is not { } inputGetter)
            return false;

        AbstractInputIcon? inputIcon = null;
        var input = inputGetter();
        if (input is VirtualButton)
        {
            inputIcon = new InputButton(name, inputGetter);
        }
        else if (input is VirtualIntegerAxis && paramList.Count > 1
            && int.TryParse(paramList[1], out int dir) && dir != 0)
        {
            inputIcon = new InputIntegerAxis(name, inputGetter)
            {
                Positive = dir >= 0
            };
        }
        else if (input is VirtualJoystick joystick && paramList.Count > 1
            && Enum.TryParse(paramList[1], ignoreCase: true, out InputJoystick.Directions direction))
        {
            inputIcon = new InputJoystick(name, inputGetter)
            {
                Direction = direction
            };
        }

        if (inputIcon is null)
            return false;

        fText.group.Nodes.Add(inputIcon);
        fText.AddWord(new string(inputIcon.SetFontCharWithButton(fText.size), 1));
        return true;
    }

    private static bool TryAddInputDirection(FancyText fText, List<string> paramList)
    {
        if (paramList.Count <= 1
            || !int.TryParse(paramList[0], out int x) || !int.TryParse(paramList[1], out int y))
            return false;

        fText.AddWord(new string(InputCharRegistry.SetDirectionToFont(new Vector2(x, y), fText.size), 1));
        return true;
    }

    private static void IL_orig_AddWord(ILContext il)
    {
        ILCursor cur = new(il);

        Logger.Info(nameof(FancyTextExtended), "Patching IL for FancyText.orig_AddWord");

        /*
                         AddFTXCharData(                            , this)
                         vvvvvvvvvvvvvvv                            vvvvvvv
         group.Nodes.Add(               new FancyText.Char { [...] }       );
         */

        cur.GotoNext(instr => instr.MatchNewobj<FancyText.Char>())
            .GotoNext(MoveType.Before,
                instr => instr.MatchCallvirt("System.Collections.Generic.List`1<Celeste.FancyText/Node>", "Add")
            );

        cur.EmitLdarg0(); // this
        cur.EmitDelegate(AddFTXCharData); // AddFTXCharData(newChar, this)
    }

    private static FancyText.Char AddFTXCharData(FancyText.Char newChar, FancyText fText)
    {
        var charData = DynamicData.For(newChar);
        var fTextData = DynamicData.For(fText);

        if (fTextData.TryGet(CurrentNoTalkField, out bool? currentNoTalk) && currentNoTalk.HasValue)
            charData.Set(Char_NoTalkField, currentNoTalk.Value);

        return newChar;
    }

    private static void On_Text_Draw(On.Celeste.FancyText.Text.orig_Draw orig, FancyText.Text text,
        Vector2 position, Vector2 justify, Vector2 scale, float alpha, int start, int end)
    {
        Text_BeforeDraw(text, start, end);
        orig(text, position, justify, scale, alpha, start, end);
    }

    private static void On_Text_DrawJustifyPerLine(On.Celeste.FancyText.Text.orig_DrawJustifyPerLine orig, FancyText.Text text,
        Vector2 position, Vector2 justify, Vector2 scale, float alpha, int start, int end)
    {
        Text_BeforeDraw(text, start, end);
        orig(text, position, justify, scale, alpha, start, end);
    }

    private static void Text_BeforeDraw(FancyText.Text text, int start, int end)
    {
        int count = Math.Min(text.Nodes.Count, end);
        for (int i = start; i < count && text.Nodes[i] is not NewPage; i++)
        {
            if (text.Nodes[i] is AbstractInputIcon inputIcon)
            {
                float charScale = 1;
                int j = i;
                for (; j < count && text.Nodes[j] is not NewPage; j++)
                    if (text.Nodes[j] is FancyText.Char @char)
                    {
                        charScale = @char.Scale;
                        break;
                    }

                float difference = inputIcon.UpdateFontCharAndGetWidthDifference(text.Font.Get(text.BaseSize), charScale);
                RecalculateCharPositions(text, j + 1, difference);
            }
        }
    }
}
