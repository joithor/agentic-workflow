using System.Globalization;
using System.Text.Json.Nodes;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Guard;
using Aw.Evidence.Core.Runs;
using Aw.Evidence.Core.Usage;
using Aw.Evidence.Testing;

namespace Aw.Evidence.Tests.Runs;

public sealed class RunFileTests
{
    private static JsonNode? Node(JsonNode root, string path) =>
        path.Split('.').Aggregate<string, JsonNode?>(root, static (node, key) =>
            int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ? node?[index] : node?[key]);

    [Fact, Trait("Lane", "unit")]
    public void A_run_summary_writes_its_schema_kebab_names_and_unknowns_as_null()
    {
        var summary = A.RunSummary().Build() with
        {
            Invocations = [new ModelInvocation(InvocationPhase.SelectorRepair, Model: null, 120, Ok: false, InputTokens: null, OutputTokens: null, CacheHit: false)],
        };

        var json = Assert.IsType<JsonObject>(JsonNode.Parse(TestJson.Write(summary)));

        Assert.Equal("ui-evidence.run/1", (string?)Node(json, "schema"));
        Assert.Equal("2026-10-10T12:00:00.0000000Z", (string?)Node(json, "startedAt"));
        Assert.Equal("manager", (string?)Node(json, "role"));
        Assert.Equal("passed", (string?)Node(json, "steps.0.status"));
        Assert.Equal("click", (string?)Node(json, "steps.0.action"));
        Assert.Equal("desktop", (string?)Node(json, "steps.0.viewport"));
        Assert.Equal("looks-right", (string?)Node(json, "visual.verdict"));
        Assert.Equal("selector-repair", (string?)Node(json, "invocations.0.phase"));
        Assert.Contains("inputTokens", Node(json, "invocations.0")?.AsObject().Select(static p => p.Key) ?? [], StringComparer.Ordinal);
        Assert.Null(Node(json, "invocations.0.inputTokens"));
        Assert.Equal("run-1", (string?)Node(json, "runId"));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_run_summary_reads_back_equal_in_content()
    {
        var summary = A.RunSummary().Build();

        var read = RunFileReader.Summary(TestJson.Utf8(TestJson.Write(summary)), TestJson.Options).Value;

        Assert.Equal(TestJson.Write(summary), TestJson.Write(read));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_parity_summary_is_an_ordered_frame_array_with_absolute_paths()
    {
        var summary = A.ParitySummary().WithProvenance(Provenance.Scrubbed)
            .WithFrame(ParitySummaryBuilder.Frame("b")).WithFrame(ParitySummaryBuilder.Frame("a")).Build();

        var json = Assert.IsType<JsonObject>(JsonNode.Parse(TestJson.Write(summary)));

        Assert.Equal("ui-evidence.parity/1", (string?)Node(json, "schema"));
        Assert.Equal("scrubbed", (string?)Node(json, "provenance"));
        Assert.Equal("b", (string?)Node(json, "frames.0.name"));
        Assert.Equal("/run/parity-1/b-diff.png", (string?)Node(json, "frames.0.files.diff"));
        Assert.Equal(false, (bool?)Node(json, "frames.0.includeAA"));
        Assert.Equal(TestJson.Write(summary), TestJson.Write(RunFileReader.Parity(TestJson.Utf8(TestJson.Write(summary)), TestJson.Options).Value));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_run_file_of_another_schema_is_refused()
    {
        var other = A.RunSummary().Build() with { Schema = "ui-evidence.run/2" };

        var result = RunFileReader.Summary(TestJson.Utf8(TestJson.Write(other)), TestJson.Options);

        Assert.Equal(EvidenceErrors.Invalid.Code, result.Error.Code);
        Assert.Contains("ui-evidence.run/1", result.Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Failed_box_checks_are_listed_per_frame() =>
        Assert.Empty(A.ParitySummary().Build().FailedBoxChecks());

    [Theory, Trait("Lane", "unit")]
    [InlineData("/run/r1", "summary.json", "/run/r1/summary.json")]
    [InlineData("/run/r1/", "/a.png", "/run/r1/a.png")]
    public void Run_paths_join_with_one_separator(string directory, string file, string expected) =>
        Assert.Equal(expected, Core.Files.RunPath.Combine(directory, file));

    [Fact, Trait("Lane", "unit")]
    public void A_content_hash_is_lower_case_sha256_hex() =>
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", ContentHash.Sha256Hex("abc"u8));
}
