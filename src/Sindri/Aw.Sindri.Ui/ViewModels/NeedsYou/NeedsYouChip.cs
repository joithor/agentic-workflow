namespace Aw.Sindri.Ui.ViewModels.NeedsYou;

/// <summary>
/// The state chip of a row: the served word and tone. A local stand-in until the contract unit's StateChip is in the
/// app (the API serves a word and a tone; the fixture has one mapping of word to colour); the list maps it then.
/// </summary>
/// <param name="Word">The chip word as served (PARKED, ESCALATED, DECISION, PROFILE, FAILED).</param>
/// <param name="Tone">The colour class.</param>
internal sealed record NeedsYouChip(string Word, NeedsYouChipTone Tone)
{
    /// <summary>No chip: a row built without one.</summary>
    internal static NeedsYouChip None { get; } = new("", NeedsYouChipTone.Neutral);
}
