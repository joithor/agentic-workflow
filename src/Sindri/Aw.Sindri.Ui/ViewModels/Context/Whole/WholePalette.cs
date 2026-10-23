namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>The bounded palette the areas are told apart by (Theme/Tokens.axaml: Brush.Area1 to Brush.Area6); more areas than colours reuse a colour with the next pattern.</summary>
internal static class WholePalette
{
    /// <summary>How many colours the palette has.</summary>
    internal const int Colours = 6;

    /// <summary>The colour and pattern of the area at a position in the order.</summary>
    /// <param name="index">The area's position.</param>
    /// <returns>The palette colour and the pattern.</returns>
    internal static (int Colour, WholePattern Pattern) For(int index)
    {
        var lap = Math.Max(0, index) / Colours;
        return (Math.Max(0, index) % Colours, (WholePattern)(lap % Enum.GetValues<WholePattern>().Length));
    }
}
