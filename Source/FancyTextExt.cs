using System;

namespace Celeste.Mod.FancyTextExtended;

public static class FancyTextExt
{
    /// <summary>
    /// Used for displaying an input icon in text
    /// </summary>
    public abstract class AbstractInputIcon(string name, Func<VirtualInput> getter) : FancyText.Node
    {
        public string Name = name;
        public Func<VirtualInput> InputGetter = getter;

        public abstract string InputName { get; }

        public abstract VirtualButton Button { get; }

        public char SetFontCharWithButton(PixelFontSize fontSize)
            => InputCharRegistry.SetInputButtonToFont(InputName, Button, fontSize);

        public float UpdateFontCharAndGetWidthDifference(PixelFontSize fontSize, float charScale = 1)
            => InputCharRegistry.UpdateAndGetWidthDifference(InputName, Button, fontSize, charScale);
    }

    public class InputButton(string name, Func<VirtualInput> getter) : AbstractInputIcon(name, getter)
    {
        public override string InputName => Name;

        public override VirtualButton Button => (VirtualButton)InputGetter();
    }

    public class InputIntegerAxis : AbstractInputIcon
    {
        public bool Positive;

        private readonly VirtualButton dummyButton;

        public InputIntegerAxis(string name, Func<VirtualInput> getter) : base(name, getter)
        {
            dummyButton = new();
            dummyButton.Deregister();
        }

        public override string InputName => Name + (Positive ? "_Positive" : "_Negative");

        public override VirtualButton Button
        {
            get
            {
                var intAxis = (VirtualIntegerAxis)InputGetter();
                dummyButton.Binding = Positive
                    ? InputExt.BindingOrAlt(intAxis.Positive, intAxis.PositiveAlt)
                    : InputExt.BindingOrAlt(intAxis.Negative, intAxis.NegativeAlt);

                return dummyButton;
            }
        }
    }

    public class InputJoystick : AbstractInputIcon
    {
        public enum Directions
        {
            Up,
            Down,
            Left,
            Right
        }

        public Directions Direction;

        private readonly VirtualButton dummyButton;

        public InputJoystick(string name, Func<VirtualInput> getter) : base(name, getter)
        {
            dummyButton = new();
            dummyButton.Deregister();
        }

        public override string InputName => Name + "_" + Direction.ToString();

        public override VirtualButton Button
        {
            get
            {
                var joystick = (VirtualJoystick)InputGetter();
                dummyButton.Binding = Direction switch
                {
                    Directions.Up => InputExt.BindingOrAlt(joystick.Up, joystick.UpAlt),
                    Directions.Down => InputExt.BindingOrAlt(joystick.Down, joystick.DownAlt),
                    Directions.Left => InputExt.BindingOrAlt(joystick.Left, joystick.LeftAlt),
                    Directions.Right => InputExt.BindingOrAlt(joystick.Right, joystick.RightAlt),
                    _ => throw new NotImplementedException()
                };

                return dummyButton;
            }
        }
    }

    /// <summary>
    /// Waits for input in the middle of the page
    /// </summary>
    public class AwaitNode : FancyText.Node
    {
        public float Position;

        public Func<bool> IsVisible = () => false;

        public float Timer;

        public void Draw(PixelFont textFont, float baseSize, Vector2 position, Vector2 textScale)
        {
            position.X += (Position * textScale.X) + ((Timer % 1 < 0.25f) ? 6 : 0);

            GFX.Gui["textboxbutton"].Draw(position, origin: Vector2.Zero, Color.White,
                scale: 1, rotation: -MathF.PI / 2f);
        }
    }

    public const string NoTalkCmd = "ftx:notalk";
    public const string EndNoTalkCmd = "/ftx:notalk";

    public const string InputCmd = "ftx:input";

    public const string AwaitCmd = "ftx:await";

    /// <summary>field for <see cref="FancyText.Char"/> of type <see cref="bool"/></summary>
    public const string Char_NoTalkField = "FancyTextExt:NoTalk";

    /// <summary>field for <see cref="FancyText"/> of type <see cref="bool"/></summary>
    public const string CurrentNoTalkField = "FancyTextExt:currentNoTalk";

    public static void RecalculateCharPositions(FancyText.Text text, int index, float difference)
    {
        if (difference == 0)
            return;

        int i = index;
        for (; i < text.Nodes.Count; i++)
        {
            if (text.Nodes[i] is FancyText.NewLine or FancyText.NewPage)
                break;
            
            if (text.Nodes[i] is FancyText.Char @char)
                @char.Position += difference;

            if (text.Nodes[i] is AwaitNode awaitNode)
                awaitNode.Position += difference;
        }
        i--;
        for (; i >= 0; i--)
        {
            if (text.Nodes[i] is FancyText.NewLine or FancyText.NewPage)
                break;

            if (text.Nodes[i] is FancyText.Char @char)
                @char.LineWidth += difference;
        }
    }
}
