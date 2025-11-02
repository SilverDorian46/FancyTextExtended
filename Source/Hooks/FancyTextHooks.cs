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
    private static readonly MethodInfo m_orig_AddWord = typeof(FancyText)
        .GetMethod("orig_" + nameof(FancyText.AddWord), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private static ILHook? hook_orig_AddWord;

    internal static void Load()
    {
        IL.Celeste.FancyText.Parse += IL_Parse;

        hook_orig_AddWord = new(m_orig_AddWord, IL_orig_AddWord);

        On.Celeste.FancyText.Text.Draw += On_Text_Draw;
        On.Celeste.FancyText.Text.DrawJustifyPerLine += On_Text_DrawJustifyPerLine;

        IL.Celeste.FancyText.Text.Draw += IL_Text_Draw;
        IL.Celeste.FancyText.Text.DrawJustifyPerLine += IL_Text_DrawJustifyPerLine;
    }

    internal static void Unload()
    {
        IL.Celeste.FancyText.Parse -= IL_Parse;

        hook_orig_AddWord?.Dispose(); hook_orig_AddWord = null;

        On.Celeste.FancyText.Text.Draw -= On_Text_Draw;
        On.Celeste.FancyText.Text.DrawJustifyPerLine -= On_Text_DrawJustifyPerLine;

        IL.Celeste.FancyText.Text.Draw -= IL_Text_Draw;
        IL.Celeste.FancyText.Text.DrawJustifyPerLine -= IL_Text_DrawJustifyPerLine;
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

        case AwaitCmd:
            fText.group.Nodes.Add(new AwaitNode
            {
                Position = fText.currentPosition
            });
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

    private static void IL_Text_Draw(ILContext il)
    {
        ILCursor cur = new(il);

        /*
         for (int j = start; j < num && !(Nodes[j] is NewPage); j++)
         {
             if (Nodes[j] is NewLine) { [...] }
             [...]
             
              <-- Text_OnDrawCurrentNode(this, j, position, scale, start);
         }
         */

        ILLabel? endOfIterationLabel = default!;

        cur.GotoNext(instr => instr.MatchStloc(6)) // j
            .GotoNext(instr => instr.MatchIsinst<FancyText.Char>())
            .GotoNext(instr => instr.MatchBrfalse(out endOfIterationLabel));

        cur.Goto(endOfIterationLabel.Target, MoveType.AfterLabel);

        cur.EmitLdarg0(); // this
        cur.EmitLdloc(6); // j
        cur.EmitLdarg1(); // position
        cur.EmitLdarg3(); // scale
        cur.EmitLdarg(5); // start
        // Text_OnDrawCurrentNode(this, j, position, scale, start)
        cur.EmitDelegate(Text_OnDrawCurrentNode);


        Logger.Info(nameof(FancyTextExtended), il.ToString());
    }

    private static void Text_OnDrawCurrentNode(FancyText.Text text, int i,
        Vector2 position, Vector2 scale, int start)
    {
        if (text.Nodes[i] is AwaitNode awaitNode && awaitNode.IsVisible())
        {
            float charHeight = 0;
            for (int j = i - 1; j >= start; j--)
            {
                Node nodeJ = text.Nodes[j];
                if (nodeJ is FancyText.Char ch)
                {
                    charHeight = (text.Font.Get(text.BaseSize).LineHeight - 8) * ch.Scale * scale.Y;
                    break;
                }
                else if (nodeJ is NewLine or NewPage)
                    break;
            }
            position.X += 8 * scale.X;
            position.Y += charHeight;

            awaitNode.Draw(text.Font, text.BaseSize, position, scale);
        }
    }

    private static void IL_Text_DrawJustifyPerLine(ILContext il)
    {
        ILCursor cur = new(il);

        /*
         for (int j = start; j < num && !(Nodes[j] is NewPage); j++)
         {
             if (Nodes[j] is NewLine) { [...] }
             [...]
             
              <-- Text_OnDrawCurrentNodeJustifyPerLine(this, j, position, scale, justify, num3, start);
         }
         */

        ILLabel? endOfIterationLabel = default!;

        cur.GotoNext(instr => instr.MatchStloc(5)) // j
            .GotoNext(instr => instr.MatchIsinst<FancyText.Char>())
            .GotoNext(instr => instr.MatchBrfalse(out endOfIterationLabel));

        cur.Goto(endOfIterationLabel.Target, MoveType.AfterLabel);

        cur.EmitLdarg0(); // this
        cur.EmitLdloc(5); // j
        cur.EmitLdarg1(); // position
        cur.EmitLdarg3(); // scale
        cur.EmitLdarg2(); // justify
        cur.EmitLdloc2(); // num3
        cur.EmitLdarg(5); // start
        // Text_OnDrawCurrentNodeJustifyPerLine(this, j, position, scale, justify, num3, start)
        cur.EmitDelegate(Text_OnDrawCurrentNodeJustifyPerLine);


        Logger.Info(nameof(FancyTextExtended), il.ToString());
    }

    // This hasn't been tested, so it's not likely to work properly.
    private static void Text_OnDrawCurrentNodeJustifyPerLine(FancyText.Text text, int i,
        Vector2 position, Vector2 scale, Vector2 justify, float heightSpan, int start)
    {
        if (text.Nodes[i] is AwaitNode awaitNode && awaitNode.IsVisible())
        {
            float charHeight = 0;
            float lineHeight = text.Font.Get(text.BaseSize).LineHeight;
            Vector2 offset = new(0, heightSpan * lineHeight);
            for (int j = i - 1; j >= start; j--)
            {
                Node nodeJ = text.Nodes[j];
                if (nodeJ is FancyText.Char ch)
                {
                    offset.X = ch.LineWidth;
                    charHeight = (lineHeight - 8) * ch.Scale * scale.Y;
                    break;
                }
                else if (nodeJ is NewLine or NewPage)
                    break;
            }
            offset = -justify * offset * scale;
            position.X += 8 * scale.X;
            position.Y += charHeight;

            awaitNode.Draw(text.Font, text.BaseSize, position + offset, scale);
        }
    }
}
