using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>
/// The one status a run shows on a board card, a list row and the run drawer (F3 workflows spec §4): the served chip's word
/// mapped to a kind, a sentence-case word, a glyph and a colour class. A word this build does not know shows as served, neutral.
/// </summary>
/// <param name="Kind">The kind of status.</param>
/// <param name="Word">The word the user reads.</param>
/// <param name="Icon">The Icons.axaml name of the glyph drawn beside the word.</param>
/// <param name="Class">The style class that colours the badge.</param>
internal sealed record RunStatus(RunStatusKind Kind, string Word, string Icon, string Class)
{
    /// <summary>Maps a served chip to the run's status.</summary>
    /// <param name="chip">The chip the API served.</param>
    /// <returns>The status to draw.</returns>
    internal static RunStatus From(StateChip chip) => throw new NotSupportedException();
}
