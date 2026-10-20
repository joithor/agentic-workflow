using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.ViewModels.Instance;

/// <summary>The children one state spawned, of one recipe: grouped by workstream when the packet has them, else the plain rows, and the count of those not listed.</summary>
internal sealed class ChildGroupViewModel
{
    internal ChildGroupViewModel(ChildGroup group, string address, Func<ChildRow, ChildRowViewModel> make)
    {
        State = group.State;
        Recipe = group.Recipe;
        var name = WorkflowNames.Of(group.Recipe, group.DisplayName);
        Header = $"{group.State} · {name}";
        Counts = Labels.ChildrenDone(group.Done, group.Total);
        Total = group.Total;
        Streams = [.. group.Workstreams.Select(w => new ChildStreamViewModel(w, make))];
        Rows = [.. group.Rows.Select(make)];
        More = group.More;
        MoreText = Labels.MoreChildren(group.More);
        OpenAllName = Labels.OpenAllInWorkName(name, address);
    }

    /// <summary>Raised when the group asks to list every child in Work.</summary>
    internal event EventHandler? WorkRequested;

    /// <summary>The state that spawned the children.</summary>
    public string State { get; }

    /// <summary>The children's recipe.</summary>
    public string Recipe { get; }

    /// <summary>"build · Step".</summary>
    public string Header { get; }

    /// <summary>"4 of 9 done".</summary>
    public string Counts { get; }

    /// <summary>How many children the group has.</summary>
    public int Total { get; }

    /// <summary>The workstreams, in the order served; empty when the children are not grouped.</summary>
    public IReadOnlyList<ChildStreamViewModel> Streams { get; }

    /// <summary>True when the children are grouped by workstream.</summary>
    public bool HasStreams => Streams.Count > 0;

    /// <summary>The children that sit in no workstream.</summary>
    public IReadOnlyList<ChildRowViewModel> Rows { get; }

    /// <summary>How many children are not listed.</summary>
    public int More { get; }

    /// <summary>True when some are not listed.</summary>
    public bool HasMore => More > 0;

    /// <summary>"… 4 more".</summary>
    public string MoreText { get; }

    /// <summary>The link's words.</summary>
#pragma warning disable CA1822, MA0041 // a fixed word is a property because the view binds it
    public string OpenAllText => Labels.OpenAllInWork;
#pragma warning restore CA1822, MA0041

    /// <summary>The link's accessible name.</summary>
    public string OpenAllName { get; }

    /// <summary>Every row of the group, workstreams first.</summary>
    internal IEnumerable<ChildRowViewModel> AllRows => Streams.SelectMany(s => s.Rows).Concat(Rows);

    /// <summary>What the group draws, so a re-read of the same keeps it.</summary>
    internal string Signature => string.Join('\n', new[] { Header, Counts, MoreText, string.Join(',', Streams.Select(s => s.Title)) }.Concat(AllRows.Select(r => r.Signature)));

    /// <summary>Asks the frame to list every child in Work, unfiltered.</summary>
    public void OpenAllInWork() => WorkRequested?.Invoke(this, EventArgs.Empty);
}
