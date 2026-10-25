namespace Aw.Sindri.Ui.ViewModels.Agents;

/// <summary>One state the session was in, as the sheet's history lists it (oldest first, the current state last).</summary>
/// <param name="Chip">The state's chip.</param>
/// <param name="Detail">The reason, the time and the source, as the sheet words them.</param>
/// <param name="IsNow">True for the current state.</param>
internal sealed record StateHistoryEntryViewModel(SessionChip Chip, string Detail, bool IsNow)
{
    /// <summary>The accessible name: the chip and its detail.</summary>
    public string Name => Chip.Text + " · " + Detail;
}
