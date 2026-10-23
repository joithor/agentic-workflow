using Aw.Sindri.Ui.Api.Context;

namespace Aw.Sindri.Ui.ViewModels.Context.Whole;

/// <summary>One feature of the map as the accessible list and the overview name it: label, size, its kinds, and the colour and pattern it is told apart by.</summary>
internal sealed class WholeAreaTileViewModel
{
    private readonly Action<string> _open;

    internal WholeAreaTileViewModel(WholeArea area, int index, Action<string> open)
    {
        _open = open;
        Key = area.Key;
        Label = area.Unassigned && area.Label.Length == 0 ? Labels.ContextWholeUnassigned : area.Label;
        MembersText = Labels.ContextWholeMembers(area.Members);
        KindsText = string.Join(", ", area.ByKind.OrderByDescending(k => k.Value).ThenBy(k => k.Key, StringComparer.Ordinal).Select(k => Labels.ContextWholeKindCount(k.Key, k.Value)));
        Name = Labels.ContextWholeTileName(Label, area.Members);
        (Colour, Pattern) = WholePalette.For(index);
    }

    /// <summary>The area's key.</summary>
    public string Key { get; }

    /// <summary>The feature's label.</summary>
    public string Label { get; }

    /// <summary>"34 items".</summary>
    public string MembersText { get; }

    /// <summary>"3 features, 5 code files".</summary>
    public string KindsText { get; }

    /// <summary>The accessible name.</summary>
    public string Name { get; }

    /// <summary>The palette colour, 0 to <see cref="WholePalette.Colours"/> - 1.</summary>
    public int Colour { get; }

    /// <summary>The pattern that tells it apart when the palette is reused.</summary>
    public WholePattern Pattern { get; }

    /// <summary>Opens the feature (Enter, click).</summary>
    public void Open() => _open(Key);
}
