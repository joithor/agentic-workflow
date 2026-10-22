using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): a phase that belongs to a line records 'shipped' or 'merged' once per effect identity in
/// 'line-report.jsonl' of its run area and replies nothing (the 'line.phaseChanged' delivery to the linked parent is the dispatcher's, later); a
/// phase with no line records nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class LineReportEffectTests
{
    private const string Instance = "T-4/phase/1";
    private const string Record = "line-report.jsonl";
    private const string LineContext = """{"line":"L1","phase":"p2","base":"origin/main","run":"line-1"}""";

    private readonly InMemoryRunArea _area = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_shipped_report_is_a_row_with_the_status_the_line_the_phase_and_the_instance()
    {
        var done = await Report("shipped", LineContext);

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        var row = JsonDocument.Parse(Assert.Single(Rows())).RootElement;
        Assert.Equal("shipped", row.GetProperty("status").GetString());
        Assert.Equal("L1", row.GetProperty("line").GetString());
        Assert.Equal("p2", row.GetProperty("phase").GetString());
        Assert.Equal(Instance, row.GetProperty("instance").GetString());
    }

    [Fact]
    public async Task A_repeated_identity_writes_one_row()
    {
        _ = await Report("merged", LineContext);

        var again = await Report("merged", LineContext);

        Assert.True(again.Value.Existed);
        _ = Assert.Single(Rows());
    }

    [Fact]
    public async Task A_phase_with_no_line_records_nothing()
    {
        var done = await Report("shipped", """{"base":"origin/main","run":"line-1"}""");

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        Assert.Empty(Rows());
    }

    [Theory]
    [InlineData("started")]
    [InlineData("")]
    public async Task A_status_other_than_shipped_or_merged_is_the_dispatch_error(string status)
    {
        var done = await Report(status, LineContext);

        Assert.True(done.IsError);
        Assert.Empty(Rows());
    }

    [Fact]
    public async Task A_line_that_is_not_a_stored_fact_is_the_dispatch_error()
    {
        var context = Context("shipped", JsonDocument.Parse("\"L1\"").RootElement.Clone());

        var done = await new LineReportEffect(_area, _facts, _clock).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    private string[] Rows() => (_area.Read(Instance, Record, Token).AsTask().GetAwaiter().GetResult()).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task<Aw.Kernel.Result<EffectDone>> Report(string status, string lineContext)
    {
        var put = (await _facts.Put(Encoding.UTF8.GetBytes(lineContext), Token)).Value;
        var stored = JsonDocument.Parse($$"""{"ref":"{{put.Ref}}","sha256":"{{put.Sha256}}"}""").RootElement.Clone();
        return await new LineReportEffect(_area, _facts, _clock).Dispatch(Context(status, stored), Token);
    }

    private static EffectContext Context(string status, JsonElement line) => new(
        BlockId.From("effect.line.report"), Instance + ":6:0", "c-1", FromEngine: false, Instance, "T-4", Attempt: 1,
        new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["status"] = JsonDocument.Parse(JsonSerializer.Serialize(status)).RootElement.Clone(),
            ["line"] = line,
        },
        new Dictionary<Aw.Sindri.Core.Workflow.Ids.FactKey, JsonElement>(), Text: null);
}
