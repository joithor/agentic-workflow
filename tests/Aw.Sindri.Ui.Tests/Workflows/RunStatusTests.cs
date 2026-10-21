using System.Xml.Linq;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Api.Workflow;
using Aw.Sindri.Ui.Tests.Theme;
using Aw.Sindri.Ui.ViewModels.Workflows;

namespace Aw.Sindri.Ui.Tests.Workflows;

/// <summary>F3 workflows spec §4: one status word per run, mapped from the served chip.</summary>
[Lane(TestLane.Unit)]
public sealed class RunStatusTests
{
    public static TheoryData<string, ChipTone, RunStatusKind, string, string> Served => new()
    {
        { "RUNNING", ChipTone.Ok, RunStatusKind.Running, "Running", "running" },
        { "DECISION", ChipTone.Attn, RunStatusKind.WaitingForYou, "Waiting for you", "attn" },
        { "ESCALATED", ChipTone.Attn, RunStatusKind.WaitingForYou, "Waiting for you", "attn" },
        { "PARKED", ChipTone.Attn, RunStatusKind.Paused, "Paused, needs you", "attn" },
        { "WITH YOU", ChipTone.Neutral, RunStatusKind.TookOver, "You took over", "violet" },
        { "QUEUED", ChipTone.Neutral, RunStatusKind.NotStarted, "Not started", "faint" },
        { "DONE", ChipTone.Neutral, RunStatusKind.Done, "Done", "ok" },
        { "FAILED", ChipTone.Fail, RunStatusKind.Failed, "Failed", "fail" },
        { "DIDN'T WORK OUT", ChipTone.Attn, RunStatusKind.DidntWorkOut, "Didn't work out", "attn" },
        { "CANCELLED", ChipTone.Neutral, RunStatusKind.Cancelled, "Cancelled", "neutral" },
    };

    [Theory]
    [MemberData(nameof(Served))]
    public void Each_served_word_maps_to_its_kind_and_sentence_case_word(string served, ChipTone tone, RunStatusKind kind, string word, string cls)
    {
        var status = RunStatus.From(new StateChip(served, tone));

        Assert.Equal(kind, status.Kind);
        Assert.Equal(word, status.Word);
        Assert.Equal(cls, status.Class);
    }

    [Fact]
    public void A_curly_apostrophe_and_lowercase_still_map()
    {
        Assert.Equal(RunStatusKind.DidntWorkOut, RunStatus.From(new StateChip("Didn’t work out", ChipTone.Attn)).Kind);
        Assert.Equal(RunStatusKind.Running, RunStatus.From(new StateChip("running", ChipTone.Ok)).Kind);
    }

    [Fact]
    public void An_unknown_word_shows_as_served_and_neutral()
    {
        var status = RunStatus.From(new StateChip("SOMETHING NEW", ChipTone.Attn));

        Assert.Equal(RunStatusKind.Unknown, status.Kind);
        Assert.Equal("SOMETHING NEW", status.Word);
        Assert.Equal("neutral", status.Class);
    }

    [Fact]
    public void Every_kind_has_a_glyph_a_word_and_the_glyph_exists_in_the_icons()
    {
        var icons = XDocument.Load(Path.Combine(UiSource.Theme, "Icons.axaml")).Descendants()
            .Select(e => e.Attributes().FirstOrDefault(a => a.Name.LocalName == "Key")?.Value)
            .OfType<string>().ToHashSet(StringComparer.Ordinal);

        foreach (var row in Served)
        {
            var status = RunStatus.From(new StateChip(row.Data.Item1, row.Data.Item2));

            Assert.False(string.IsNullOrWhiteSpace(status.Word));
            Assert.Contains("Icon." + status.Icon, icons);
        }

        Assert.Contains("Icon." + RunStatus.From(new StateChip("SOMETHING NEW", ChipTone.Neutral)).Icon, icons);
    }

    [Fact]
    public void A_status_never_leans_on_colour_alone_failed_cancelled_and_didnt_work_out_are_told_apart_by_word()
    {
        var words = new[] { "FAILED", "DIDN'T WORK OUT", "CANCELLED" }
            .Select(w => RunStatus.From(new StateChip(w, ChipTone.Neutral)).Word).ToList();

        Assert.Equal(3, words.Distinct(StringComparer.Ordinal).Count());
    }
}
