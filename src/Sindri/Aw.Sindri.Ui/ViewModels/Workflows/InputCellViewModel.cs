namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>One declared input of a Workflows row (screens §4.2): its label with a check when present or a cross when missing.</summary>
internal sealed class InputCellViewModel
{
    /// <summary>The input's served label.</summary>
    public string Label { get; } = string.Empty;

    /// <summary>True when the input is present.</summary>
    public bool Present { get; }

    /// <summary>The check or the cross.</summary>
    public string Mark { get; } = string.Empty;

    /// <summary>The name screen readers hear: the label and whether it is present.</summary>
    public string Name { get; } = string.Empty;
}
