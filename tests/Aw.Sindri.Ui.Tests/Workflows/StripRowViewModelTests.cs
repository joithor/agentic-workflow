using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One line of the Home Workflows strip (screens §4.1, E-20): what it shows, read from the root golden's rows.</summary>
[Lane(TestLane.Unit)]
public sealed class StripRowViewModelTests
{
    private readonly FakeClipboard _clipboard = new();
    private readonly FakeTimeProvider _clock = new(FixtureInstances.Rows.Max(r => r.Since) + TimeSpan.FromMinutes(90));

    private StripRowViewModel Of(InstanceSummary row) => new(row, _clipboard, _clock);

    private static InstanceSummary Where(Func<InstanceSummary, bool> predicate) => FixtureInstances.Rows.First(predicate);

    private static StateCell Current(InstanceSummary row) => row.States.First(s => string.Equals(s.State, row.State, StringComparison.Ordinal));

    [Fact]
    public void Shows_chip_subject_recipe_stage_tag_now_and_age()
    {
        var row = Where(r => Current(r).Round is not null && r.Recipe.Urgency == RecipeUrgency.Incident);

        var vm = Of(row);

        Assert.Equal((row.Chip, row.Subject.Label, WorkflowNames.Of(row.Recipe.Id, row.Recipe.DisplayName), StageTag.For(row), row.Now), (vm.Chip, vm.Subject, vm.Workflow, vm.Stage, vm.Now));
        Assert.Equal(Labels.RowAge(_clock.GetUtcNow() - row.Since), vm.Age);
    }

    [Fact]
    public void Chip_word_comes_from_the_one_chip_table()
        => Assert.All(FixtureInstances.Rows, r => Assert.Equal((r.Chip.Word, r.Chip.Tone), (Of(r).Chip.Word, Of(r).Chip.Tone)));

    [Fact]
    public void Now_is_the_served_sentence_unchanged()
        => Assert.All(FixtureInstances.Rows, r => Assert.Equal(r.Now, Of(r).Now));

    [Fact]
    public void A_subject_without_a_label_shows_its_key()
    {
        var row = Where(r => r.Subject.Label.Length > 0) with { Subject = new SubjectRef(Kind: "item", Key: "K-1", Label: "", Href: null) };

        Assert.Equal("K-1", Of(row).Subject);
    }

    [Fact]
    public void Engine_badge_replaces_the_glyph_in_the_stage_tag()
    {
        var row = Where(r => r.EngineOwned is { Kind: EngineOwnedKind.Escalated });

        var vm = Of(row);

        Assert.Equal($"▲ escalated: {row.EngineOwned!.Reason}", vm.Badge); // why: the row was chosen by its badge
        Assert.Equal("▲", vm.Glyph);
        Assert.True(vm.HasBadge);
    }

    [Fact]
    public void A_row_with_no_badge_has_the_plain_glyph_and_an_incident_has_the_alert_one()
    {
        var plain = Of(Where(r => r.EngineOwned is null && r.Recipe.Urgency == RecipeUrgency.Work));
        var incident = Of(Where(r => r.Recipe.Urgency == RecipeUrgency.Incident));

        Assert.Equal((false, "", Labels.MarkActive, Labels.MarkEscalated), (plain.HasBadge, plain.Badge, plain.Glyph, incident.Glyph));
    }

    [Fact]
    public void Desk_state_with_an_answer_action_shows_Answer()
    {
        var row = Where(r => Current(r).Desk && r.Actions.Contains("CompleteStep", StringComparer.Ordinal) && r.Answer is null);

        var vm = Of(row);

        Assert.True(vm.CanAnswer);
        Assert.Equal(Labels.Answer, vm.AnswerText);
        Assert.Null(vm.Protected);
        Assert.Equal($"{Labels.Answer}, {row.Subject.Label}", vm.AnswerName);
    }

    [Fact]
    public void Protected_answer_shows_the_lock_button_with_its_popover()
    {
        var row = Where(r => r.Answer is not null && string.Equals(r.Answer.Class, "protected", StringComparison.Ordinal));

        var vm = Of(row);

        Assert.False(vm.CanAnswer);
        Assert.NotNull(vm.Protected);
        Assert.Equal(row.Answer!.Cli, vm.Protected.Cli); // why: the row was chosen by its answer
        Assert.StartsWith("⌂ ", vm.Protected.Label, StringComparison.Ordinal);
        Assert.EndsWith(" · terminal", vm.Protected.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void Row_without_a_ready_action_shows_none()
    {
        var taken = Of(Where(r => r.EngineOwned is { Kind: EngineOwnedKind.TakenOver }));
        var finished = Of(Where(r => r.Actions.Count == 0));

        Assert.All([taken, finished], vm => Assert.Equal((false, null), (vm.CanAnswer, vm.Protected)));
    }

    [Fact]
    public void Row_name_is_the_subject_then_the_workflow_and_the_address_stays_the_rows_key()
    {
        var row = Where(r => r.Subject.Label.Length > 0 && r.Recipe.Urgency != RecipeUrgency.Incident);

        var vm = Of(row);

        Assert.Equal((row.Address, $"{row.Subject.Label}, {vm.Workflow}"), (vm.Address, vm.AccessibleName));
        Assert.Equal(vm.AccessibleName, vm.OpenName);
        Assert.StartsWith(vm.Subject, vm.OpenName, StringComparison.Ordinal);
        Assert.DoesNotContain(row.Address, vm.AccessibleName, StringComparison.Ordinal);
        Assert.DoesNotContain(row.Address, vm.AnswerName, StringComparison.Ordinal);
        Assert.Equal($"Answer, {vm.Subject}", vm.AnswerName);
    }
}
