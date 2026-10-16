#pragma warning disable CA1822, MA0041, IDE0021, MA0003, CS0067, CS9113, IDE0060, CA1801 // red stub: the members throw until the green commit
using Aw.Sindri.Ui.Api.Workflow;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The recipe's states, as the API serves them.</summary>
internal sealed class StatesBandViewModel
{
    internal StatesBandViewModel(IReadOnlyList<StateCell> cells, PathOrdering ordering, string current) => throw new NotSupportedException();

    /// <summary>The states in served order, branches folded under their source.</summary>
    public IReadOnlyList<StateNodeViewModel> Nodes => throw new NotSupportedException();

    /// <summary>The header note when the recipe has no path; empty otherwise.</summary>
    public string OrderNote => throw new NotSupportedException();

    /// <summary>True when there is an order note.</summary>
    public bool HasOrderNote => throw new NotSupportedException();
}
