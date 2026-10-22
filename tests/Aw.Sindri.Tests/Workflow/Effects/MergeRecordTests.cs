using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>The merge record's writer (PB spec §4.7, BD7): append-only, one row per effect identity, the fixed columns on every row.</summary>
[Lane(TestLane.Unit)]
public sealed class MergeRecordTests
{
    private const string Instance = "T-4/pr-babysitter/1";

    private readonly InMemoryRunArea _area = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private MergeRecord Record => new(_area, _clock);

    [Fact]
    public async Task A_row_carries_its_kind_identity_instance_head_time_and_fields()
    {
        var written = await Record.Append(Row("merge", "i:1:0", new JsonObject { ["method"] = "squash", ["sha"] = "m1" }), Token);

        Assert.True(written.Value);
        var row = Single(await Lines());
        Assert.Equal("merge", row.GetProperty("kind").GetString());
        Assert.Equal("i:1:0", row.GetProperty("identity").GetString());
        Assert.Equal(Instance, row.GetProperty("instance").GetString());
        Assert.Equal("h1", row.GetProperty("headSha").GetString());
        Assert.Equal("squash", row.GetProperty("method").GetString());
        Assert.True(row.TryGetProperty("at", out _));
    }

    [Fact]
    public async Task A_row_with_the_same_identity_is_not_written_again()
    {
        _ = await Record.Append(Row("push", "i:1:0"), Token);

        var again = await Record.Append(Row("push", "i:1:0"), Token);

        Assert.False(again.Value);
        _ = Single(await Lines());
    }

    [Fact]
    public async Task Rows_with_other_identities_are_appended_in_order_and_the_earlier_ones_are_untouched()
    {
        _ = await Record.Append(Row("start", "i:1:0"), Token);
        var first = await Lines();

        _ = await Record.Append(Row("end", "i:9:0"), Token);

        var lines = await Lines();
        Assert.Equal(2, lines.Length);
        Assert.Equal(first[0], lines[0]);
        Assert.Equal("end", JsonDocument.Parse(lines[1]).RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task A_field_cannot_overwrite_a_fixed_column()
    {
        _ = await Record.Append(Row("docs", "i:1:0", new JsonObject { ["kind"] = "end", ["instance"] = "elsewhere" }), Token);

        var row = Single(await Lines());
        Assert.Equal("docs", row.GetProperty("kind").GetString());
        Assert.Equal(Instance, row.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task A_kind_the_record_does_not_know_is_an_error_and_writes_nothing()
    {
        var written = await Record.Append(Row("reply", "i:1:0"), Token);

        Assert.True(written.IsError);
        Assert.Empty(await Lines());
    }

    private static MergeRow Row(string kind, string identity, JsonObject? fields = null) => new(Instance, identity, kind, "h1", fields ?? []);

    private async Task<string[]> Lines() => (await _area.Read(Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static JsonElement Single(string[] lines) => JsonDocument.Parse(Assert.Single(lines)).RootElement.Clone();
}
