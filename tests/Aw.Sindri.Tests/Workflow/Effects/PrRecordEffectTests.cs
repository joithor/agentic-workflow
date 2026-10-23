using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>'effect.pr.record' (PB spec §4.7): appends a 'recheck', 'docs' or 'end' row to the merge record, once per effect identity.</summary>
[Lane(TestLane.Unit)]
public sealed class PrRecordEffectTests
{
    private readonly PrEffectRig _rig = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_recheck_row_carries_the_kind_the_result_the_instance_and_the_head()
    {
        var done = await Record("recheck");

        Assert.False(done.Existed);
        Assert.Null(done.Reply);
        var row = JsonDocument.Parse(Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))).RootElement;
        Assert.Equal("recheck", row.GetProperty("kind").GetString());
        Assert.Equal(PrEffectRig.Instance, row.GetProperty("instance").GetString());
        Assert.Equal("h1", row.GetProperty("headSha").GetString());
        Assert.Equal("resolved", row.GetProperty("result").GetProperty("verdict").GetString());
    }

    [Fact]
    public async Task A_repeated_identity_writes_one_row()
    {
        _ = await Record("docs");

        var again = await Record("docs");

        Assert.True(again.Existed);
        _ = Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public async Task A_kind_the_record_does_not_know_is_the_dispatch_error()
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "push", ["result"] = PrEffectRig.Fact("{}") });

        var done = await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    [Fact]
    public async Task An_end_row_carries_the_terminal_the_instance_and_the_head()
    {
        var done = await End("merged", notes: null);

        Assert.False(done.Existed);
        var row = await ReadRow();
        Assert.Equal("end", row.GetProperty("kind").GetString());
        Assert.Equal("merged", row.GetProperty("terminal").GetString());
        Assert.Equal(PrEffectRig.Instance, row.GetProperty("instance").GetString());
        Assert.Equal("h1", row.GetProperty("headSha").GetString());
        Assert.False(row.TryGetProperty("reworkNotesHash", out _));
    }

    [Fact]
    public async Task A_rework_end_row_carries_the_hash_of_the_rework_notes_not_the_notes()
    {
        _ = await End("rework", """{"text":"split the phase"}""");

        var row = await ReadRow();
        var hash = row.GetProperty("reworkNotesHash").GetString();
        Assert.StartsWith("sha256:", hash, StringComparison.Ordinal);
        Assert.Equal(71, hash!.Length);
        Assert.DoesNotContain("split the phase", row.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_repeated_end_identity_writes_one_row()
    {
        _ = await End("closed", notes: null);

        var again = await End("closed", notes: null);

        Assert.True(again.Existed);
        _ = Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Theory]
    [InlineData("ready")]
    [InlineData("")]
    public async Task An_end_terminal_the_recipe_does_not_have_is_the_dispatch_error(string terminal)
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "end", ["terminal"] = terminal });

        var done = await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    [Fact]
    public async Task A_rework_end_without_notes_is_the_dispatch_error()
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "end", ["terminal"] = "rework" });

        var done = await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    [Theory]
    [InlineData("doneByHand")]
    [InlineData("left")]
    public async Task A_docsMerge_row_carries_the_owner_desk_outcome_the_trigger_and_the_docs_pr(string outcome)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "docsMerge", ["outcome"] = outcome, ["trigger"] = "ownerDesk" };
        var facts = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["fact:prState"] = PrEffectRig.PrState("h1", "h1"),
            ["fact:docsMerge"] = """{"docsPr":"https://example.test/o/r/pull/9","outcome":"pending","sha":null}""",
        };

        var done = await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(_rig.Context("effect.pr.record", arguments, facts), Token);

        Assert.False(done.Value.Existed);
        var row = await ReadRow();
        Assert.Equal("docsMerge", row.GetProperty("kind").GetString());
        Assert.Equal(outcome, row.GetProperty("outcome").GetString());
        Assert.Equal("ownerDesk", row.GetProperty("trigger").GetString());
        Assert.Equal("https://example.test/o/r/pull/9", row.GetProperty("docsPr").GetString());
        Assert.Equal("h1", row.GetProperty("headSha").GetString());
    }

    [Fact]
    public async Task A_docsMerge_outcome_the_desk_does_not_have_is_the_dispatch_error()
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "docsMerge", ["outcome"] = "merged" });

        var done = await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    private async Task<JsonElement> ReadRow() =>
        JsonDocument.Parse(Assert.Single((await _area.Read(PrEffectRig.Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries))).RootElement;

    private async Task<Core.Workflow.Blocks.EffectDone> End(string terminal, string? notes)
    {
        var arguments = new Dictionary<string, object>(StringComparer.Ordinal) { ["kind"] = "end", ["terminal"] = terminal };
        if (notes is not null)
        {
            arguments["notes"] = PrEffectRig.Fact(notes);
        }

        var context = _rig.Context("effect.pr.record", arguments, new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prState"] = PrEffectRig.PrState("h1", "h1") });
        return (await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token)).Value;
    }

    private async Task<Core.Workflow.Blocks.EffectDone> Record(string kind)
    {
        var context = _rig.Context("effect.pr.record", new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["kind"] = kind,
            ["result"] = PrEffectRig.Fact("""{"verdict":"resolved"}"""),
        }, new Dictionary<string, string>(StringComparer.Ordinal) { ["fact:prState"] = PrEffectRig.PrState("h1", "h1") });
        return (await new PrRecordEffect(new MergeRecord(_area, _clock), _rig.Facts).Dispatch(context, Token)).Value;
    }
}
