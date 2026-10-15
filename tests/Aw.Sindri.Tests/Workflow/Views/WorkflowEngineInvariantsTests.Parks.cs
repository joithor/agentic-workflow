using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Invariants 8 and 9: one declaration per recipe hash, and the answer a park takes (WF 6.4).</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [Fact, Trait("Lane", "unit")]
    public void An_instance_shows_the_declared_states_loops_with_their_cap_and_the_declared_reaction_to_a_parked_child() => Walk(Declared);

    private static List<string> Declared(View view, RecipeShape shape)
    {
        var failures = new List<string>();
        if (!view.Cells.Select(c => c.State).SequenceEqual(shape.States.Select(s => s.Id), StringComparer.Ordinal))
        {
            failures.Add("the state list differs from the declaration");
        }

        failures.AddRange(view.Cells.SelectMany(cell => CellFailures(cell, shape)));
        failures.AddRange(ParkedChild(view, StateOf(view, shape)));
        return failures;
    }

    private static List<string> CellFailures(StateCell cell, RecipeShape shape)
    {
        var state = shape.State(cell.State);
        var flags = cell.Desk != state.Desk || cell.Child != state.Child || cell.Terminal != state.Terminal || cell.Order != state.Order || cell.BranchOf != state.BranchOf;
        return [.. flags ? [M($"{cell.State}: desk/child/terminal/order differ from the declaration")] : Array.Empty<string>(), .. RoundFailures(cell, shape)];
    }

    private static List<string> RoundFailures(StateCell cell, RecipeShape shape)
    {
        var failures = new List<string>();
        var caps = shape.Loops.Where(l => l.From == cell.State || l.To == cell.State).Select(l => l.Max).ToList();
        if (cell.Round is { } round && !caps.Contains(round.Max))
        {
            failures.Add(M($"{cell.State}: round {round.N}/{round.Max}, the declared loops here have cap {string.Join('/', caps)}"));
        }

        if (!cell.Loops.All(l => shape.Loops.Any(d => d.Max == l.Max && ((d.From == cell.State && d.To == l.With) || (d.To == cell.State && d.From == l.With)))))
        {
            failures.Add(M($"{cell.State}: a loop pair the declaration doesn't have"));
        }

        return failures;
    }

    private static string Declares(ShapeState state) => state.OnParked?.ToString().ToLowerInvariant() ?? "(not a child wait)";

    private static List<string> ParkedChild(View view, ShapeState state)
    {
        var failures = new List<string>();
        if (view.EngineOwned is { Kind: EngineOwnedKind.Parked } parked && parked.Reason.Contains("(child)", StringComparison.Ordinal) && state.OnParked != OnParked.Propagate)
        {
            failures.Add(M($"parked by a child, but '{view.State}' declares onParked: {Declares(state)}"));
        }

        var mirrored = view.Detail?.Children.SelectMany(RowsOf).Any(r => r.MirroredPark is not null) ?? false;
        if (view.EngineOwned is null && mirrored && state.OnParked != OnParked.Ignore)
        {
            failures.Add(M($"a parked child shown while the parent keeps running, but '{view.State}' declares onParked: {Declares(state)}"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parks_answer_follows_its_reason_and_whether_the_state_is_a_wait_or_a_step() => Walk(Answers);

    private static (AnswerKind? Kind, string? Option) Expected(EngineOwnedBadge owned, bool wait) => owned switch
    {
        { Kind: EngineOwnedKind.Escalated } => (AnswerKind.Accept, "--accept"),
        { Reason: var r } when r.StartsWith("timeout", StringComparison.Ordinal) || r.Contains("SND-WF-010", StringComparison.Ordinal) => (AnswerKind.Resume, "--resume"),
        { Reason: var r } when r.StartsWith("spec-conflict", StringComparison.Ordinal) => (null, null),
        { Reason: var r } when r.StartsWith("no-progress", StringComparison.Ordinal) || r.StartsWith("rounds", StringComparison.Ordinal) => (AnswerKind.Grant, "--grant"),
        { Reason: var r } when r.StartsWith("budget", StringComparison.Ordinal) => (AnswerKind.GrantTokens, "--grant-tokens"),
        { Reason: var r } when r.StartsWith("needs-answer", StringComparison.Ordinal) => wait ? (AnswerKind.Resume, "--resume [--text") : (AnswerKind.Text, "--text"),
        _ => throw new InvalidOperationException($"the park reason '{owned.Reason}' has no answer rule (WF 6.4)"),
    };

    private static List<string> Answers(View view, RecipeShape shape)
    {
        if (view.EngineOwned is not { Kind: EngineOwnedKind.Parked or EngineOwnedKind.Escalated } owned)
        {
            return [];
        }

        var wait = StateOf(view, shape).Kind == StateKind.Wait;
        var (kind, option) = Expected(owned, wait);
        return [.. view.Statuses.Where(s => !Takes(s, kind, option)).Select(s => M($"park '{owned.Reason}' at {(wait ? "a wait" : "a Step")}: answer {s.Answer?.Kind.ToString() ?? "none"} '{s.Answer?.Cli}', WF 6.4 gives {kind?.ToString() ?? "none"} {option}"))];
    }

    private static bool Takes(StatusFields status, AnswerKind? kind, string? option) =>
        status.Answer?.Kind == kind && (option is null || (status.Answer?.Cli.Contains(option, StringComparison.Ordinal) ?? false));
}
