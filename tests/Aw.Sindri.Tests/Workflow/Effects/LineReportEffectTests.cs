using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.line.report' (line spec BD7, LN-WF1, Q13): a phase that belongs to a line records 'shipped' or 'merged' once per effect identity in
/// 'line-report.jsonl' of its run area (the audit trail) and replies the status with 'fact:phaseEvent' { line, phase, status, instance, at }, which
/// the 'line.phaseChanged' event carries to the line; a phase with no line records and replies nothing.
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

        Assert.Equal(OutcomeName.From("shipped"), done.Value.Reply);
        Assert.False(done.Value.Existed);
        var row = JsonDocument.Parse(Assert.Single(Rows())).RootElement;
        Assert.Equal("shipped", row.GetProperty("status").GetString());
        Assert.Equal("L1", row.GetProperty("line").GetString());
        Assert.Equal("p2", row.GetProperty("phase").GetString());
        Assert.Equal(Instance, row.GetProperty("instance").GetString());
    }

    [Theory]
    [InlineData("shipped")]
    [InlineData("merged")]
    [InlineData("failed")]
    [InlineData("parked")]
    [InlineData("resumed")]
    [InlineData("ended")]
    public async Task A_report_replies_its_status_with_the_phase_storedfact(string status)
    {
        var done = await Report(status, LineContext);

        Assert.Equal(OutcomeName.From(status), done.Value.Reply);
        Assert.Equal("line.phaseChanged", done.Value.Event?.Value);   // 'merged' is also pr.mergeResult's and pr.docsMerged's outcome
        var fact = Assert.Single(done.Value.Facts!);
        Assert.Equal(FactKey.From("fact:phaseEvent"), fact.Key);
        var stored = await _facts.Read(new FactValue(fact.Value.Ref, fact.Value.Sha256, fact.Value.HeadSha), Token);
        var payload = JsonDocument.Parse(stored.Value).RootElement;
        Assert.Equal("L1", payload.GetProperty("line").GetString());
        Assert.Equal("p2", payload.GetProperty("phase").GetString());
        Assert.Equal(status, payload.GetProperty("status").GetString());
        Assert.Equal(Instance, payload.GetProperty("instance").GetString());
        Assert.Equal(JsonDocument.Parse(Assert.Single(Rows())).RootElement.GetProperty("at").GetString(), payload.GetProperty("at").GetString());
    }

    [Fact]
    public async Task A_repeated_identity_writes_one_row_and_replies_the_same_storedagain()
    {
        var first = await Report("merged", LineContext);
        _clock.Advance(TimeSpan.FromMinutes(5));

        var again = await Report("merged", LineContext);

        Assert.True(again.Value.Existed);
        Assert.Equal(OutcomeName.From("merged"), again.Value.Reply);
        Assert.Equal(Assert.Single(first.Value.Facts!).Value.Sha256, Assert.Single(again.Value.Facts!).Value.Sha256);
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
    public async Task A_status_the_line_does_not_track_is_the_dispatch_error(string status)
    {
        var done = await Report(status, LineContext);

        Assert.True(done.IsError);
        Assert.Empty(Rows());
    }

    [Fact]
    public async Task An_engine_report_of_a_linked_phase_carries_its_attempt_park_reason_outcome_and_packet()
    {
        var put = (await _facts.Put(Encoding.UTF8.GetBytes(LineContext), Token)).Value;
        var stored = JsonDocument.Parse($$"""{"ref":"{{put.Ref}}","sha256":"{{put.Sha256}}"}""").RootElement.Clone();
        var context = Context("ended", stored, new Dictionary<string, string> { ["ended"] = "resolved", ["packet"] = "sha256:abc", ["parkReason"] = "budget" }, "T-4/line/1/phase.p2.3");

        var done = await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(context, Token);

        var fact = Assert.Single(done.Value.Facts!).Value;
        var payload = JsonDocument.Parse((await _facts.Read(new FactValue(fact.Ref, fact.Sha256, fact.HeadSha), Token)).Value).RootElement;
        Assert.Equal(3, payload.GetProperty("attempt").GetInt32());
        Assert.Equal("resolved", payload.GetProperty("ended").GetString());
        Assert.Equal("sha256:abc", payload.GetProperty("packet").GetString());
        Assert.Equal("budget", payload.GetProperty("parkReason").GetString());
    }

    [Fact]
    public async Task The_phase_is_the_packets_when_the_report_passes_one_not_the_line_contexts_or_the_addresss_slug()
    {
        var line = (await _facts.Put(Encoding.UTF8.GetBytes(LineContext), Token)).Value;
        var packet = (await _facts.Put(Encoding.UTF8.GetBytes("""{"phase":"P2","id":"p2"}"""), Token)).Value;
        var context = Context("parked", JsonDocument.Parse($$"""{"ref":"{{line.Ref}}","sha256":"{{line.Sha256}}"}""").RootElement.Clone(), instance: "T-4/line/1/phase.p2.1") with
        {
            Arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["status"] = JsonDocument.Parse("\"parked\"").RootElement.Clone(),
                ["line"] = JsonDocument.Parse($$"""{"ref":"{{line.Ref}}","sha256":"{{line.Sha256}}"}""").RootElement.Clone(),
                ["phasePacket"] = JsonDocument.Parse($$"""{"ref":"{{packet.Ref}}","sha256":"{{packet.Sha256}}"}""").RootElement.Clone(),
            },
        };

        var done = await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(context, Token);

        var fact = Assert.Single(done.Value.Facts!).Value;
        var payload = JsonDocument.Parse((await _facts.Read(new FactValue(fact.Ref, fact.Sha256, fact.HeadSha), Token)).Value).RootElement;
        Assert.Equal("P2", payload.GetProperty("phase").GetString());
    }

    [Fact]
    public async Task A_line_that_is_not_a_stored_fact_is_the_dispatch_error()
    {
        var context = Context("shipped", JsonDocument.Parse("\"L1\"").RootElement.Clone());

        var done = await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    private string[] Rows() => _area.Read(Instance, Record, Token).AsTask().GetAwaiter().GetResult().Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task<Kernel.Result<EffectDone>> Report(string status, string lineContext)
    {
        var put = (await _facts.Put(Encoding.UTF8.GetBytes(lineContext), Token)).Value;
        var stored = JsonDocument.Parse($$"""{"ref":"{{put.Ref}}","sha256":"{{put.Sha256}}"}""").RootElement.Clone();
        return await new LineReportEffect(_area, _facts, _facts, _clock).Dispatch(Context(status, stored), Token);
    }

    private static EffectContext Context(string status, JsonElement line, Dictionary<string, string>? extras = null, string instance = Instance)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["status"] = JsonDocument.Parse(JsonSerializer.Serialize(status)).RootElement.Clone(),
            ["line"] = line,
        };
        foreach (var (name, text) in extras ?? [])
        {
            arguments[name] = JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();
        }

        return new(BlockId.From("effect.line.report"), instance + ":6:0", "c-1", FromEngine: false, instance, "T-4", Attempt: 1, arguments, new Dictionary<FactKey, JsonElement>(), Text: null);
    }
}
