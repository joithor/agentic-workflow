namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>The action bar asks to answer a decision: the row, the option and the note sent with it.</summary>
/// <param name="rowId">The decision row.</param>
/// <param name="optionId">The option chosen.</param>
/// <param name="note">The reason, or empty for none.</param>
internal sealed class NeedsYouDecisionEventArgs(string rowId, string optionId, string note) : EventArgs
{
    /// <summary>The decision row.</summary>
    public string RowId { get; } = rowId;

    /// <summary>The option chosen.</summary>
    public string OptionId { get; } = optionId;

    /// <summary>The reason, or empty for none.</summary>
    public string Note { get; } = note;

    /// <summary>Set by the handler: completes with the refusal the decision came back with, or empty when it was accepted or nothing was sent.</summary>
    public Task<string> Settled { get; set; } = Task.FromResult("");
}
