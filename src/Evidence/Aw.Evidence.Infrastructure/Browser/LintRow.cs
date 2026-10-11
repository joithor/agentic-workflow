namespace Aw.Evidence.Infrastructure.Browser;

/// <summary>One lint element as the page script returns it.</summary>
internal sealed class LintRow
{
    public string Selector { get; set; } = "";

    public double X { get; set; }

    public double Y { get; set; }

    public double W { get; set; }

    public double H { get; set; }

    public string Text { get; set; } = "";

    public string? TextOverflow { get; set; }

    public string? Overflow { get; set; }

    public string? Color { get; set; }

    public bool HasHoverState { get; set; }

    public bool HasFocusState { get; set; }
}
