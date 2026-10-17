using System.Globalization;
using System.Text.RegularExpressions;
using Aw.Sindri.Core.Workflow.Views;
using Aw.Sindri.Host.Catalog;
using Aw.Sindri.Testing.Views;

namespace Aw.Sindri.Tests.Workflow.Views;

/// <summary>
/// The golden files are the contract the UI lanes build on, so the fixtures behind them must agree with themselves: each
/// instance's status names that instance, the status text says what its fields say, and the commands are catalog rows.
/// </summary>
public sealed class WorkflowViewFixtureTests
{
    public static TheoryData<string> Details()
    {
        var rows = new TheoryData<string>();
        foreach (var detail in WorkflowViewFixtures.AllDetails())
        {
            rows.Add(detail.Address.Value);
        }

        return rows;
    }

    private static InstanceDetail Detail(string address) => WorkflowViewFixtures.AllDetails().Single(d => d.Address.Value == address);

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Details))]
    public void An_instance_view_carries_its_own_status(string address)
    {
        var view = Detail(address);
        var status = view.Current.Status;

        Assert.Equal(view.Address, status.Header.Address);
        Assert.Equal(view.State, view.Current.State);
        Assert.Equal(view.Recipe.Id, status.Header.Recipe);
        Assert.Equal(view.Recipe.Hash.Display, status.Header.Hash12);
        Assert.Equal(view.Recipe.Version, status.Header.Version);
        Assert.Equal(view.Since, status.Header.Since);
        Assert.All(status.Commands, command =>
        {
            Assert.EndsWith(view.Address.Value, command.Cli.Contains(" --", StringComparison.Ordinal) ? command.Cli[..command.Cli.IndexOf(" --", StringComparison.Ordinal)] : command.Cli, StringComparison.Ordinal);
            Assert.Contains(command.ActionId, view.Actions);
        });
        Assert.Equal(view.Actions.Count(a => a.Value != "OpenItem"), status.Commands.Count);
    }

    [Theory, Trait("Lane", "unit")]
    [MemberData(nameof(Details))]
    public void The_cancel_sentence_names_the_instance_and_counts_its_open_children(string address)
    {
        var view = Detail(address);
        var cancel = view.Current.Status.Commands.SingleOrDefault(c => c.Verb == StatusVerb.Cancel);
        if (cancel is null)
        {
            Assert.DoesNotContain(view.Actions, a => a.Value == "ApplyCommand:cancel");
            return;
        }

        var open = view.Children.Sum(g => g.Total - g.Done);
        var match = Regex.Match(cancel.Confirm!, @"^Cancel (?<address>\S+)\? (?<n>\d+) running steps? stops?; (?<m>\d+) (?:children are|child is) cancelled\.$", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(1));
        Assert.True(match.Success, cancel.Confirm);
        Assert.Equal(view.Address.Value, match.Groups["address"].Value);
        Assert.Equal(open, int.Parse(match.Groups["m"].Value, CultureInfo.InvariantCulture));
        Assert.All(view.Current.Status.Commands.Where(c => c.Verb != StatusVerb.Cancel), c => Assert.Null(c.Confirm));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_status_text_says_what_its_fields_say()
    {
        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            var lines = status.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            var header = status.Header;
            Assert.StartsWith($"{header.Address.Value}  {header.Recipe.Value}@{header.Hash12}  v{header.Version.ToString(CultureInfo.InvariantCulture)}  {header.State}", lines[0], StringComparison.Ordinal);
            Assert.Equal(status.Next is { Length: > 0 }, HasLine(lines, "next"));
            Assert.Equal(status.Child is not null, HasLine(lines, "child"));
            Assert.Equal(status.Drift is not null, HasLine(lines, "drift"));
            Assert.Equal(status.Inputs.Count > 0, HasLine(lines, "inputs"));
            Assert.Equal(status.Timeout is not null, HasLine(lines, "timeout"));
            Assert.Equal(status.Budget is not null, HasLine(lines, "budget"));
            Assert.Equal(status.Paused is not null, HasLine(lines, "paused"));
            Assert.Equal(status.Answer is not null || status.Drift is not null, HasLine(lines, "answer"));   // a drift park prints "answer  none (drift): ..."
            Assert.Equal(status.Commands.Count > 0, HasLine(lines, "commands"));
            if (status.Answer is { } answer)
            {
                Assert.Contains(answer.Cli, status.Text, StringComparison.Ordinal);
            }

            foreach (var command in status.Commands)
            {
                Assert.Contains(StatusVerbNames.Of(command.Verb), lines.Single(l => l.StartsWith("  commands", StringComparison.Ordinal)), StringComparison.Ordinal);
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_status_command_and_answer_is_a_catalog_row_of_the_same_class()
    {
        var catalog = EngineRows.All.ToDictionary(r => r.Action.Value, r => r.Class, StringComparer.Ordinal);
        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            foreach (var command in status.Commands)
            {
                Assert.True(catalog.TryGetValue(command.ActionId.Value, out var actionClass), $"{command.ActionId.Value} is not a catalog row");
                Assert.Equal(actionClass, command.Class);
            }

            if (status.Answer is { } answer)
            {
                Assert.True(catalog.TryGetValue(answer.ActionId.Value, out var actionClass), $"{answer.ActionId.Value} is not a catalog row");
                Assert.Equal(actionClass, answer.Class);
                if (answer.Via is not null)
                {
                    Assert.DoesNotContain(status.Commands, c => c.Verb == StatusVerb.Answer);   // a child pointer serves no answer entry
                    continue;
                }

                Assert.Equal(answer.ActionId, status.Commands.First(c => c.Verb == StatusVerb.Answer).ActionId);   // the status answer's variant is the first served
            }
        }
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_fixture_status_names_each_verb_once_on_its_commands_line()
    {
        foreach (var status in WorkflowViewFixtures.AllStatuses())
        {
            var line = status.Text.Split('\n').Select(static l => l.Trim()).FirstOrDefault(static l => l.StartsWith("commands", StringComparison.Ordinal));
            if (line is null)
            {
                continue;
            }

            var verbs = line["commands".Length..].Split(',', StringSplitOptions.TrimEntries);
            Assert.Equal(verbs.Distinct(StringComparer.Ordinal).ToArray(), verbs);
        }

        Assert.Contains(WorkflowViewFixtures.AllStatuses(), static s => s.Commands.Where(static c => c.Verb == StatusVerb.Answer).Skip(1).Any());   // the repeated-answer shapes are in the set
    }

    [Fact, Trait("Lane", "unit")]
    public void Main_path_states_are_numbered_from_one_and_a_branch_has_no_order()
    {
        foreach (var view in WorkflowViewFixtures.AllDetails())
        {
            var main = view.States.Where(s => s.BranchOf is null).Select(s => s.Order).ToList();
            Assert.Equal(Enumerable.Range(1, main.Count).Cast<int?>(), main);
            Assert.All(view.States.Where(s => s.BranchOf is not null), s => Assert.Null(s.Order));
        }

        Assert.Null(Detail("ITM-01JL3/fork-demo/1").States.Single(s => s.State == "right").Order);
    }

    private static bool HasLine(string[] lines, string label) => lines.Any(l => l.StartsWith($"  {label} ", StringComparison.Ordinal));
}
