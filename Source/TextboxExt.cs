using System;
using System.Collections.Generic;

namespace Celeste.Mod.FancyTextExtended;

public static class TextboxExt
{
    public static FancyText.Node? GetCurrentNode(this Textbox textbox)
    {
        List<FancyText.Node> nodes = textbox.Nodes;
        if (textbox.index >= nodes.Count)
            return null;

        return nodes[textbox.index];
    }
}
