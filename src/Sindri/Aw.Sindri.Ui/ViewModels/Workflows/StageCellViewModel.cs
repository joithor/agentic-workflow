namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One stage cell of a Workflows row (screens §4.2): a glyph, or the engine badge or the blocked dash in its place, then what the cell serves.</summary>
internal sealed class StageCellViewModel
{
    /// <summary>The state this cell is.</summary>
    public string State { get; } = string.Empty;

    /// <summary>The leading mark: the progress glyph, the engine badge, or the blocked dash.</summary>
    public string Glyph { get; } = string.Empty;

    /// <summary>What follows the mark: the round, desk, child or progress.</summary>
    public string Detail { get; } = string.Empty;

    /// <summary>The cell's words, the mark then the detail.</summary>
    public string Text { get; } = string.Empty;

    /// <summary>True when a required input is missing and this is the state the instance waits in.</summary>
    public bool IsBlocked { get; }

    /// <summary>True for the state the instance is in.</summary>
    public bool IsCurrent { get; }

    /// <summary>The name screen readers hear: the state and what the cell says.</summary>
    public string Name { get; } = string.Empty;
}
