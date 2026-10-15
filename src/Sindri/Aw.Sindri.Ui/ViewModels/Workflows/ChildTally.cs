using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

#pragma warning disable MA0182 // until the U8 pages (T1, T4) place it
/// <summary>What one group of children counts to, taken from its rows: the figures a head or a state cell prints.</summary>
/// <param name="Done">The rows whose state is done.</param>
/// <param name="Total">The rows shown and the rows the group says it left out.</param>
/// <param name="ByWord">The rows per chip word, in the order the words first appear.</param>
internal sealed record ChildTally(int Done, int Total, IReadOnlyList<(string Word, int Rows)> ByWord)
{
    /// <summary>The tally of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>Its counts.</returns>
    internal static ChildTally Of(ChildGroup group) => throw new NotSupportedException();
}
#pragma warning restore MA0182
