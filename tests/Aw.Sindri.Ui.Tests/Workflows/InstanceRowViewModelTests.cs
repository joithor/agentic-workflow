using System.Globalization;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Testing;
using Aw.Sindri.Ui.ViewModels;
using Aw.Sindri.Ui.ViewModels.Workflows;
using Microsoft.Extensions.Time.Testing;
using StateGlyph = Aw.Sindri.Ui.Api.Workflow.StateGlyph;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>One row of the Workflows list (screens §4.2, U8 T1): every cell is a served field mapped to words; nothing is worked out from the recipe.</summary>
[Lane(TestLane.Unit)]
public sealed class InstanceRowViewModelTests
{
    private static InstanceRowViewModel Row(InstanceSummary row) => new(row, new FakeClipboard(), new FakeTimeProvider(A.Noon));

    private static IReadOnlyList<InstanceSummary> Open => FixtureInstances.List("open").Instances;

    private static InstanceSummary Where(Func<InstanceSummary, bool> match) => Open.First(match);

    private static string Mark(StateGlyph glyph) => glyph switch
    {
        StateGlyph.Done => Labels.MarkDone,
        StateGlyph.Active => Labels.MarkActive,
        StateGlyph.Pending => Labels.MarkPending,
        _ => throw new ArgumentOutOfRangeException(nameof(glyph), glyph, message: null),
    };

    [Fact]
    public void Subject_cell_is_the_served_label() => Assert.All(Open, served => Assert.Equal(served.Subject.Label, Row(served).Subject));

    [Fact]
    public void Inputs_show_check_or_cross_per_declared_input()
    {
        Assert.All(Open, served =>
        {
            var cells = Row(served).Inputs;
            Expect.Sequence(served.Inputs.Select(i => i.Label), cells.Select(c => c.Label));
            Expect.Sequence(served.Inputs.Select(i => i.Present ? Labels.MarkInputPresent : Labels.MarkInputMissing), cells.Select(c => c.Mark));
            Assert.All(cells.Zip(served.Inputs), p => Assert.Contains(p.Second.Present ? "present" : "missing", p.First.Name, StringComparison.Ordinal));
        });
        var missing = FixtureInstances.List("preview-missing-input").Instances[0];
        Assert.Contains(Row(missing).Inputs, c => !c.Present && c.Mark == Labels.MarkInputMissing);
    }

    [Fact]
    public void A_missing_required_input_blanks_its_stage_as_blocked_dash()
    {
        var served = FixtureInstances.List("preview-missing-input").Instances[0];
        Assert.Contains(served.Inputs, i => i is { Required: true, Present: false });

        var stages = Row(served).Stages;

        var blocked = Assert.Single(stages, s => s.IsBlocked);
        Assert.Equal((served.State, Labels.BlockedMark, true), (blocked.State, blocked.Glyph, blocked.IsCurrent));
        Assert.StartsWith(Labels.BlockedMark, blocked.Text, StringComparison.Ordinal);
        Assert.Contains(Labels.EngineBadge(served.EngineOwned!.Kind, served.EngineOwned.Reason), blocked.Text, StringComparison.Ordinal);
        Assert.All(Open, other => Assert.DoesNotContain(Row(other).Stages, s => s.IsBlocked));
    }

    [Fact]
    public void Stage_cell_shows_glyph_then_round_desk_child_or_progress()
    {
        Assert.All(Open.Where(r => r.EngineOwned is null), served =>
        {
            var stages = Row(served).Stages;
            Expect.Sequence(served.States.Select(s => s.State), stages.Select(s => s.State));
            Assert.All(served.States.Zip(stages), p =>
            {
                var (cell, stage) = p;
                Assert.StartsWith(Mark(cell.Glyph), stage.Text, StringComparison.Ordinal);
                Assert.Equal(cell.Round is { } r ? string.Create(CultureInfo.InvariantCulture, $"r{r.N}/{r.Max}") : string.Empty, Between(stage, "r"), StringComparer.Ordinal);
                Assert.Equal(cell.Desk, stage.Detail.Contains("desk", StringComparison.Ordinal));
                Assert.Equal(cell.Child && cell.Children is null, stage.Detail.Contains("child", StringComparison.Ordinal));
                Assert.Equal(cell.Children is { } c ? string.Create(CultureInfo.InvariantCulture, $"{c.Done}/{c.Total}") : string.Empty, Between(stage, "n"), StringComparer.Ordinal);
            });
            _ = Assert.Single(stages, s => s.IsCurrent);
        });

        // The cells that carry each part exist in the fixture, so the loop above checks them.
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Round is not null);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Desk);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Child && s.Children is null);
        Assert.Contains(Open.SelectMany(r => r.States), s => s.Children is not null);
    }

    // The round ("r1/3") or the progress ("4/9") token of a cell's detail; empty when it has none.
    private static string Between(StageCellViewModel stage, string kind) =>
        stage.Detail.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(t => kind == "r" ? t.StartsWith('r') && t.Contains('/', StringComparison.Ordinal) : char.IsDigit(t[0]) && t.Contains('/', StringComparison.Ordinal)) ?? string.Empty;

    [Fact]
    public void Engine_badge_replaces_the_glyph()
    {
        var served = Where(r => r.EngineOwned is { Kind: EngineOwnedKind.Escalated });
        var covered = served.States.Single(s => s.Badge is not null);

        var stage = Row(served).Stages.Single(s => s.State == covered.State);

        Assert.Equal(Labels.EngineBadge(covered.Badge!.Kind, covered.Badge.Reason), stage.Glyph);
        Assert.StartsWith(stage.Glyph, stage.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(Mark(covered.Glyph), stage.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void Taken_over_row_shows_WITH_YOU_and_the_badge_on_the_covered_state()
    {
        var served = Where(r => r.EngineOwned is { Kind: EngineOwnedKind.TakenOver });
        var covered = served.States.Single(s => s.Badge is not null);
        var row = Row(served);

        var stage = row.Stages.Single(s => s.State == covered.State);

        Assert.Equal(served.Chip.Word, row.Chip.Word);
        Assert.Equal(served.State, covered.State);
        Assert.Contains(covered.Badge!.Reason, stage.Glyph, StringComparison.Ordinal);
        Assert.DoesNotContain(Mark(covered.Glyph), stage.Text, StringComparison.Ordinal);
        _ = Assert.Single(row.Stages, s => s.Glyph.Contains(covered.Badge.Reason, StringComparison.Ordinal));
    }

    [Fact]
    public void Now_is_the_served_now_verbatim() => Assert.All(Open, served => Assert.Equal(served.Now, Row(served).Now));

    [Fact]
    public void Chip_is_the_served_chip() => Assert.All(Open, served => Assert.Equal(served.Chip, Row(served).Chip));

    [Fact]
    public void The_name_carries_the_state_and_the_chip_and_the_current_cell_says_current()
    {
        Assert.All(Open, served =>
        {
            var row = Row(served);
            Assert.Contains(served.State, row.AccessibleName, StringComparison.Ordinal);
            Assert.Contains(served.Chip.Word, row.AccessibleName, StringComparison.Ordinal);
            Assert.Contains(row.Subject, row.AccessibleName, StringComparison.Ordinal);
            var current = row.Stages.Single(s => s.State == served.State);
            Assert.EndsWith(", " + Labels.StageCurrent, current.Name, StringComparison.Ordinal);
            Assert.All(row.Stages.Where(s => s.State != served.State), s => Assert.DoesNotContain(Labels.StageCurrent, s.Name, StringComparison.Ordinal));
            Assert.Equal(Labels.OpenInstanceName(served.Address), row.OpenName);
        });
    }
}
