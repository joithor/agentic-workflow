using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>
/// The recipe's states, in the order the API serves them (screens §4.2). A branch folds under the state it branches off;
/// a link between two states is the loop mark when they loop into each other, else the step link. Each link belongs to
/// the state before it, so a wrap never starts a line with one. The band works nothing out from the recipe.
/// </summary>
internal sealed class StatesBandViewModel
{
    /// <param name="cells">The served states.</param>
    /// <param name="ordering">How they were ordered.</param>
    /// <param name="current">The state the instance is in.</param>
    /// <param name="nameOf">A state id in the recipe's words; null reads the ids as served.</param>
    internal StatesBandViewModel(IReadOnlyList<StateCell> cells, PathOrdering ordering, string current, Func<string, string>? nameOf = null)
    {
        var main = cells.Where(c => c.BranchOf is null).ToList();
        Nodes = [.. main.Select((cell, i) => Node(cells, cell, i + 1 < main.Count ? main[i + 1] : null, current, nameOf))];
        OrderNote = ordering == PathOrdering.TransitionList ? Labels.OrderedByTransitions : string.Empty;
    }

    /// <summary>The states in served order, branches folded under their source.</summary>
    public IReadOnlyList<StateNodeViewModel> Nodes { get; }

    /// <summary>The header note when the recipe has no path; empty otherwise.</summary>
    public string OrderNote { get; }

    /// <summary>True when there is an order note.</summary>
    public bool HasOrderNote => OrderNote.Length > 0;

    /// <summary>The band's heading.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string Heading => Labels.StatesHeading;
#pragma warning restore CA1822, MA0041

    /// <summary>What the band draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', OrderNote, string.Join(';', Nodes.Select(n => n.Signature)));

    private static StateNodeViewModel Node(IReadOnlyList<StateCell> all, StateCell cell, StateCell? next, string current, Func<string, string>? nameOf)
    {
        var branches = all.Where(c => string.Equals(c.BranchOf, cell.State, StringComparison.Ordinal)).Select(c => new StateNodeViewModel(c, IsCurrent(c, current), string.Empty, [], loopsWith: null, nameOf)).ToList();
        var loops = next is not null && Loops(cell, next);
        return new StateNodeViewModel(cell, IsCurrent(cell, current), next is null ? string.Empty : loops ? Labels.LoopMark : Labels.StepLink, branches, loops ? next?.State : null, nameOf);
    }

    private static bool IsCurrent(StateCell cell, string current) => string.Equals(cell.State, current, StringComparison.Ordinal);

    // Two states loop into each other when either names the other; a state that loops into itself is not a pair.
    private static bool Loops(StateCell a, StateCell b) =>
        !string.Equals(a.State, b.State, StringComparison.Ordinal)
        && (a.Loops.Any(l => string.Equals(l.With, b.State, StringComparison.Ordinal)) || b.Loops.Any(l => string.Equals(l.With, a.State, StringComparison.Ordinal)));
}
