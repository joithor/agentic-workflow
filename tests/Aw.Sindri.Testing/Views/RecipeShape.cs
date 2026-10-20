#pragma warning disable MA0003, MA0127 // fixture records are positional; their literals are named by the record
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Testing.Views;

/// <summary>
/// One recipe at one hash, declared once: its stamp, states, loops, inputs and outputs. Every instance, list row, fact row and
/// recipe view of the fixtures is built from it, so no two of them can disagree. The recipes that have a file under recipes/ are
/// compared with it in the tests.
/// </summary>
/// <param name="Id">The recipe id.</param>
/// <param name="Version">The version.</param>
/// <param name="HashPrefix">The hash, padded with zeros to 64 hex digits.</param>
/// <param name="Urgency">The urgency.</param>
/// <param name="States">The states in path order.</param>
/// <param name="Loops">The loop edges.</param>
/// <param name="Inputs">The inputs.</param>
/// <param name="Outputs">The outputs.</param>
/// <param name="BudgetTokens">The recipe-wide token budget, where declared.</param>
/// <param name="Name">The name template, where declared (spec section 3.11).</param>
public sealed record RecipeShape(string Id, int Version, string HashPrefix, RecipeUrgency Urgency, IReadOnlyList<ShapeState> States,
    IReadOnlyList<ShapeLoop> Loops, IReadOnlyList<ShapeInput> Inputs, IReadOnlyList<ShapeOutput> Outputs, int? BudgetTokens = null, string? Name = null)
{
    /// <summary>The declaration of one state.</summary>
    /// <param name="id">The state id.</param>
    /// <returns>The state.</returns>
    public ShapeState State(string id) => States.Single(s => s.Id == id);

    /// <summary>The display name the fixtures give a recipe: its id after "workflow." with the words apart and the first capitalised.</summary>
    /// <param name="id">The recipe id.</param>
    /// <returns>The name.</returns>
    public static string Named(string id)
    {
        var words = id[(id.IndexOf('.', StringComparison.Ordinal) + 1)..].Split('-', StringSplitOptions.RemoveEmptyEntries);
        var text = string.Join(' ', words);
        return string.Concat(char.ToUpperInvariant(text[0]).ToString(), text.AsSpan(1));
    }

    /// <summary>The stamp every instance of this recipe carries.</summary>
    public InstanceRecipe Stamp => new(RecipeId.From(Id), RecipeHash.From("sha256:" + HashPrefix.PadRight(64, '0')), Version, Urgency, Named(Id));

    /// <summary>The state cells, path order, with <paramref name="current"/> active and earlier states done.</summary>
    /// <param name="current">The current state.</param>
    /// <param name="round">The loop round the instance is in.</param>
    /// <param name="change">A per-instance change to a cell.</param>
    /// <returns>The cells.</returns>
    public IReadOnlyList<StateCell> Cells(string current, int round = 0, Func<StateCell, StateCell>? change = null)
    {
        var at = States.ToList().FindIndex(s => s.Id == current);
        var cells = new List<StateCell>();
        for (var i = 0; i < States.Count; i++)
        {
            var s = States[i];
            var glyph = i < at || (s.Terminal && at == i) ? StateGlyph.Done : i == at ? StateGlyph.Active : StateGlyph.Pending;
            var loops = Loops.SelectMany(l => new[] { (State: l.From, With: l.To, l.Max), (State: l.To, With: l.From, l.Max) })
                .Where(p => p.State == s.Id).Select(p => new LoopPair(p.With, round, p.Max)).DistinctBy(p => p.With, StringComparer.Ordinal).ToList();
            var cell = new StateCell(s.Id, s.Order, s.BranchOf, glyph, null, null, loops, s.Desk, s.Child, null, null, s.Terminal);
            cells.Add(change is null ? cell : change(cell));
        }

        return cells;
    }

    /// <summary>The input slots of a list row.</summary>
    /// <param name="missing">The keys of required inputs not yet given.</param>
    /// <returns>One slot per declared input.</returns>
    public IReadOnlyList<InputPresence> InputRows(params string[] missing) =>
        [.. Inputs.Select(i => new InputPresence(i.Key, i.Label ?? Plain(i.Key), i.Kind, i.Generate is null, !missing.Contains(i.Key, StringComparer.Ordinal)))];

    /// <summary>One fact row: label, type, direction and producing state come from the declaration.</summary>
    /// <param name="key">A declared input or output key.</param>
    /// <param name="status">The status.</param>
    /// <param name="summary">The summary.</param>
    /// <param name="at">When it was recorded.</param>
    /// <param name="by">The actor.</param>
    /// <param name="reference">The reference.</param>
    /// <param name="progress">The progress.</param>
    /// <param name="pending">What a pending fact waits for.</param>
    /// <returns>The row.</returns>
    public FactRow Fact(string key, FactStatus status, string summary, DateTimeOffset? at = null, string? by = null, FactRef? reference = null,
        Progress? progress = null, FactPending? pending = null) =>
        FactRowOf(key, status, summary, at, by, reference, progress, pending);

    private FactRow FactRowOf(string key, FactStatus status, string summary, DateTimeOffset? at, string? by, FactRef? reference, Progress? progress, FactPending? pending)
    {
        if (Inputs.FirstOrDefault(i => i.Key == key) is { } input)
        {
            return new FactRow(key, input.Label ?? Plain(key), input.Type, FactDirection.Input, null, status, summary, at, by, reference, progress, pending);
        }

        var output = Outputs.Single(o => o.Key == key);
        return new FactRow(key, output.Label, output.Type, FactDirection.Output, output.From, status, summary, at, by, reference, progress, pending);
    }

    /// <summary>The complete fact-row list of an instance: every input (it must be given), then every output (not started unless given).</summary>
    /// <param name="given">The rows the instance has.</param>
    /// <returns>The rows in declaration order.</returns>
    public IReadOnlyList<FactRow> Facts(params FactRow[] given) =>
    [
        .. Inputs.Select(i => given.Single(g => g.Key == i.Key)),
        .. Outputs.Select(o => given.SingleOrDefault(g => g.Key == o.Key) ?? Fact(o.Key, FactStatus.Pending, "not started")),
    ];

    /// <summary>The recipe's view (E-22).</summary>
    /// <returns>The view.</returns>
    public RecipeView View() => new(
        Stamp.Id, Stamp.Hash, Version, Name,
        [.. Inputs.Select(i => new RecipeInputView(i.Key, i.Label, i.Type, i.Kind, i.Generate is null, i.Generate))],
        [.. Outputs.Select(o => new RecipeOutputView(o.Key, o.Label, o.Type, o.From))],
        Urgency, States.Any(s => s.BranchOf is not null) ? PathOrdering.TransitionList : PathOrdering.Path, [.. States.Select(s => new RecipeStateView(s.Id, s.Order, s.BranchOf))], [.. Loops.Select(l => new RecipeLoopView(l.Name, l.From, l.To, l.Max))], Stamp.DisplayName);

    private static string Plain(string key) => key[(key.IndexOf(':', StringComparison.Ordinal) + 1)..];
}
