using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One state of the band: its name, the mark or engine badge, and what the cell serves (round, desk, child, progress, outcome).</summary>
internal sealed class StateNodeViewModel
{
    private readonly StageCellViewModel _cell;

    /// <param name="cell">The served cell.</param>
    /// <param name="isCurrent">True for the state the instance is in.</param>
    /// <param name="link">What joins this state to the next.</param>
    /// <param name="branches">The states that branch off this one.</param>
    /// <param name="loopsWith">The state it loops with, or null.</param>
    /// <param name="nameOf">A state id in the recipe's words; null reads the ids as served.</param>
    internal StateNodeViewModel(StateCell cell, bool isCurrent, string link, IReadOnlyList<StateNodeViewModel> branches, string? loopsWith = null, Func<string, string>? nameOf = null)
    {
        _cell = new StageCellViewModel(cell, isCurrent, isBlocked: false, nameOf?.Invoke(cell.State));
        IsCurrent = isCurrent;
        IsTerminal = cell.Terminal;
        Outcome = cell.Outcome ?? string.Empty;
        Link = link;
        Branches = branches;
        Name = string.Concat(
            _cell.Name,
            Outcome.Length > 0 ? ", " + Outcome : string.Empty,
            loopsWith is { Length: > 0 } ? ", " + Labels.LoopsWith(nameOf?.Invoke(loopsWith) ?? loopsWith) : string.Empty,   // the loop mark and the indent are glyphs: the words say what they show
            cell.BranchOf is { Length: > 0 } source ? ", " + Labels.BranchOf(nameOf?.Invoke(source) ?? source) : string.Empty);
    }

    /// <summary>The state.</summary>
    public string State => _cell.State;

    /// <summary>The state in words: the recipe's name for it when served, else its id.</summary>
    public string StateName => _cell.StateName;

    /// <summary>The state, its mark and what it serves, then its outcome.</summary>
    public string Text => $"{StateName} {_cell.Text}{(Outcome.Length > 0 ? " " + Outcome : string.Empty)}";

    /// <summary>The words screen readers hear: the state in words, its outcome, what it loops with, what it branches from.</summary>
    public string Name { get; }

    /// <summary>True for the state the instance is in.</summary>
    public bool IsCurrent { get; }

    /// <summary>True for a terminal state.</summary>
    public bool IsTerminal { get; }

    /// <summary>The state's served outcome, or empty.</summary>
    public string Outcome { get; }

    /// <summary>What joins this state to the next: the step link, or the loop mark when the two loop into each other; empty on the last.</summary>
    public string Link { get; }

    /// <summary>The states that branch off this one.</summary>
    public IReadOnlyList<StateNodeViewModel> Branches { get; }

    /// <summary>True when states branch off this one.</summary>
    public bool HasBranches => Branches.Count > 0;

    /// <summary>What the node draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('|', Name, Text, Link, string.Join(',', Branches.Select(b => b.Signature)));
}
