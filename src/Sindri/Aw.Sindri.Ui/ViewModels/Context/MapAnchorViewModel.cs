namespace Aw.Sindri.Ui.ViewModels.Context;

/// <summary>One starting point in the "N starting points" list.</summary>
/// <param name="Key">The anchor's key.</param>
/// <param name="Label">Its label.</param>
/// <param name="Kind">Its kind word as served.</param>
internal sealed record MapAnchorViewModel(string Key, string Label, string Kind)
{
    /// <summary>The kind in plain words, for the caption.</summary>
    public string KindWord => Labels.ContextMapKindWord(Kind);
}
