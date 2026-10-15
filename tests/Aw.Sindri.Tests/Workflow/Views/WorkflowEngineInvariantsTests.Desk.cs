using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>Invariants 4 and 5: the desk runtime's run tokens, and a state with no agent runtime served as a desk task.</summary>
public sealed partial class WorkflowEngineInvariantsTests
{
    [GeneratedRegex(@"--run (?<entry>\d+)\.(?<run>\d+)", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex RunOption { get; }

    [Fact, Trait("Lane", "unit")]
    public void A_desk_run_token_is_the_entry_of_its_state_then_the_run_number_and_every_text_names_the_same_one() => Walk(Tokens);

    private static List<string> TokensOf(View view)
    {
        var tokens = new List<string>();
        if (view.Detail?.Current.Desk is { } desk)
        {
            tokens.Add(desk.Run.Value);
        }

        foreach (var status in view.Statuses)
        {
            var texts = status.Commands.Select(c => c.Cli).Append(status.Text);
            tokens.AddRange(texts.SelectMany(t => RunOption.Matches(t)).Select(m => M($"{m.Groups["entry"].Value}.{m.Groups["run"].Value}")));
        }

        return [.. tokens.Distinct(StringComparer.Ordinal)];
    }

    private static List<string> Tokens(View view, RecipeShape shape)
    {
        if (StateOf(view, shape).Order is not { } order)
        {
            return [];
        }

        var looped = view.Cells.Any(c => c.Round is { N: > 1 });
        var tokens = TokensOf(view);
        var failures = tokens
            .Select(t => int.Parse(t[..t.IndexOf('.', StringComparison.Ordinal)], CultureInfo.InvariantCulture))
            .Where(entry => entry != order && !(looped && entry > order))
            .Select(entry => M($"run token entry {entry}: the entry of '{view.State}' is {order} (RunToken = <entry>.<n>, WF 5.3); tokens {string.Join(", ", tokens)}"))
            .ToList();
        if (tokens.Count > 1)
        {
            failures.Add(M($"different run tokens: {string.Join(", ", tokens)}"));
        }

        return failures;
    }

    [Fact, Trait("Lane", "unit")]
    public void A_state_with_no_agent_runtime_is_served_as_a_desk_task_with_a_complete_command_and_a_decision_chip() => Walk(DeskTasks);

    private static List<string> DeskTasks(View view, RecipeShape shape)
    {
        var state = StateOf(view, shape);
        var failures = new List<string>();
        if (CellOf(view).Desk != state.Desk)
        {
            failures.Add("the cell's desk flag differs from the declaration");
        }

        failures.AddRange(ChildDeskRows(view));
        if (state.RunsAtDesk && view.EngineOwned is null)
        {
            failures.AddRange(DeskView(view));
        }

        return failures;
    }

    private static List<string> ChildDeskRows(View view)
    {
        if (view.Detail is not { } detail)
        {
            return [];
        }

        return [.. detail.Children.SelectMany(group =>
        {
            var child = FixtureRecipes.All.Single(s => s.Id == group.Recipe.Value);
            return RowsOf(group)
                .Where(r => r.Glyph == StateGlyph.Active && r.EngineOwned is null && child.State(r.State).RunsAtDesk && (r.Chip.Word != "DECISION" || r.Agent is not null))
                .Select(r => M($"child {r.Address.Value} at '{r.State}' is a desk task: chip {r.Chip.Word}, agent '{r.Agent}'"));
        })];
    }

    private static List<string> DeskView(View view)
    {
        var failures = new List<string>();
        if (view.Chip is { } chip && chip.Word != "DECISION")
        {
            failures.Add(M($"chip {chip.Word}, a desk task is DECISION"));
        }

        if (view.Detail is { } detail && detail.Current.Desk is null)
        {
            failures.Add("no DeskForm");
        }

        foreach (var status in view.Statuses)
        {
            if (status.Header.Detail != "desk task")
            {
                failures.Add(M($"status detail '{status.Header.Detail}', not 'desk task'"));
            }

            if (status.Commands.Count == 0 || status.Commands[0].Verb.Value != "complete")
            {
                failures.Add(M($"status commands start with '{(status.Commands.Count == 0 ? "nothing" : status.Commands[0].Verb.Value)}', not complete"));
            }
        }

        return failures;
    }
}
