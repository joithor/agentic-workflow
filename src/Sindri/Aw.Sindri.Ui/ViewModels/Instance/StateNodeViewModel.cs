#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>One state of the band.</summary>
internal sealed class StateNodeViewModel
{
    internal StateNodeViewModel() => throw new NotSupportedException();

    /// <summary>The state.</summary>
    public string State => throw new NotSupportedException();

    /// <summary>The state, its mark and what it serves.</summary>
    public string Text => throw new NotSupportedException();

    /// <summary>The words screen readers hear.</summary>
    public string Name => throw new NotSupportedException();

    /// <summary>True for the state the instance is in.</summary>
    public bool IsCurrent => throw new NotSupportedException();

    /// <summary>True for a terminal state.</summary>
    public bool IsTerminal => throw new NotSupportedException();

    /// <summary>The state's served outcome, or empty.</summary>
    public string Outcome => throw new NotSupportedException();

    /// <summary>What joins this state to the next: the step link, or the loop mark; empty on the last.</summary>
    public string Link => throw new NotSupportedException();

    /// <summary>The states that branch off this one.</summary>
    public IReadOnlyList<StateNodeViewModel> Branches => throw new NotSupportedException();
}
