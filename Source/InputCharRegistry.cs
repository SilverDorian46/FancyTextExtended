using System;
using System.Xml;

namespace Celeste.Mod.FancyTextExtended;

public static class InputCharRegistry
{
    private static XmlElement? fakeXml;

    private static XmlElement FakeXml => fakeXml ??= new XmlDocument().CreateElement("input");

    public static string GetInputEmojiName(string name) => "FTXInput:" + name;

    public static char GetOrRegisterInputChar(string name)
    {
        string emojiName = GetInputEmojiName(name);
        if (!Emoji.TryGet(emojiName, out char inputChar))
        {
            Emoji.Register(emojiName, GFX.Game["__fallback"]);
            inputChar = (char)(Emoji.Start + Emoji.Get(emojiName));
        }
        return inputChar;
    }

    public static char SetInputButtonToFont(string name, VirtualButton button, PixelFontSize fontSize)
    {
        char inputChar = GetOrRegisterInputChar(name);
        SetInputCharToFont(inputChar, Input.GuiButton(button, Input.PrefixMode.Latest), fontSize);

        return inputChar;
    }

    public static void SetInputCharToFont(char inputChar, MTexture texture, PixelFontSize fontSize)
    {
        if (fontSize.Characters.TryGetValue(inputChar, out var pixelFontChar))
            UpdateInputFontChar(pixelFontChar, texture);
        else
            CreateAndAddInputFontChar(inputChar, texture, fontSize);
    }

    private static void UpdateInputFontChar(PixelFontCharacter fontChar, MTexture texture)
    {
        fontChar.Texture = texture;
        fontChar.XAdvance = (int)(texture.Width * texture.ScaleFix);
    }

    private static PixelFontCharacter CreateAndAddInputFontChar(char inputChar, MTexture texture, PixelFontSize fontSize)
    {
        XmlElement fakeXml = FakeXml;
        fakeXml.SetAttr("x", 0);
        fakeXml.SetAttr("y", 0);
        fakeXml.SetAttr("width", texture.Width);
        fakeXml.SetAttr("height", texture.Height);
        fakeXml.SetAttr("xoffset", 0);
        fakeXml.SetAttr("yoffset", 0);
        fakeXml.SetAttr("xadvance", (int)(texture.Width * texture.ScaleFix));

        var pixelFontChar = new PixelFontCharacter(inputChar, texture, fakeXml);
        fontSize.Characters.Add(inputChar, pixelFontChar);

        return pixelFontChar;
    }

    public static float UpdateAndGetWidthDifference(string name, VirtualButton button, PixelFontSize fontSize,
        float charScale = 1)
        => UpdateAndGetWidthDifference(GetOrRegisterInputChar(name),
            Input.GuiButton(button, Input.PrefixMode.Latest), fontSize, charScale);

    public static float UpdateAndGetWidthDifference(char inputChar, MTexture newTexture, PixelFontSize fontSize,
        float charScale = 1)
    {
        float prevWidth;
        if (fontSize.Characters.TryGetValue(inputChar, out var pixelFontChar))
        {
            prevWidth = pixelFontChar.XAdvance * charScale;
            UpdateInputFontChar(pixelFontChar, newTexture);
        }
        else
        {
            prevWidth = 0;
            pixelFontChar = CreateAndAddInputFontChar(inputChar, newTexture, fontSize);
        }
        return (pixelFontChar.XAdvance * charScale) - prevWidth;
    }

    public static string GetDirectionEmojiName(Vector2 direction)
        => $"FTXDirection:{Math.Sign(direction.X)}x{Math.Sign(direction.Y)}";

    public static char GetOrRegisterDirectionChar(Vector2 direction)
    {
        string emojiName = GetDirectionEmojiName(direction);
        if (!Emoji.TryGet(emojiName, out char inputChar))
        {
            Emoji.Register(emojiName, GFX.Game["__fallback"]);
            inputChar = (char)(Emoji.Start + Emoji.Get(emojiName));
        }
        return inputChar;
    }

    public static char SetDirectionToFont(Vector2 direction, PixelFontSize fontSize)
    {
        char inputChar = GetOrRegisterDirectionChar(direction);
        SetInputCharToFont(inputChar, Input.GuiDirection(direction), fontSize);

        return inputChar;
    }
}
