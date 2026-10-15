using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.Testing;

/// <summary>
/// A consistency check, not app code: what one group's shown rows add up to, to hold against the figures the API serves
/// (ChildGroup.Done and .Total), which the views print as served. Nothing in the app recounts them.
/// </summary>
/// <param name="Done">The shown rows whose state is done.</param>
/// <param name="Total">The rows shown and the rows the group says it left out.</param>
/// <param name="ByWord">The shown rows per chip word, in the order the words first appear.</param>
internal sealed record ChildTally(int Done, int Total, IReadOnlyList<(string Word, int Rows)> ByWord)
{
    /// <summary>The tally of a group's shown rows.</summary>
    /// <param name="group">The group.</param>
    /// <returns>Its counts.</returns>
    internal static ChildTally Of(ChildGroup group)
    {
        var rows = FixtureInstances.ChildRows(group).ToList();
        var byWord = rows.GroupBy(r => r.Chip.Word, StringComparer.Ordinal).Select(g => (g.Key, g.Count())).ToList();
        return new ChildTally(rows.Count(r => r.Glyph == StateGlyph.Done), rows.Count + group.More, byWord);
    }
}
