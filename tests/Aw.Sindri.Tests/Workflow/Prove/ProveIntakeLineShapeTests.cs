using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Prove;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Workflow.Prove;

/// <summary>The prove station's intake over the signals.json the line writes (a flat list of <c language="text">{item, kind, statement, runs}</c>; api's e2e run28 stop on step.prove.intake).</summary>
[Lane(TestLane.Unit)]
public sealed class ProveIntakeLineShapeTests
{
    private const string Dir = "/packets/p1";

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    // Trimmed from the real phase packet of run28: three items (one superseded), the signals as the line wrote them (no id, 'runs').
    private const string Acceptance = """{"schemaVersion":1,"parent":"sha256:bb","items":[{"id":"AC-1","text":"list prints two lines","status":"failing","evidence":[]},{"id":"AC-7","text":"the 15 existing cases pass","status":"failing","evidence":[]},{"id":"AC-9","text":"old","status":"superseded","evidence":[]}]}""";

    private const string Signals = """{"schemaVersion":1,"signals":[{"item":"AC-1","kind":"bash-test","statement":"list on the repo's binaries.lock prints exactly 2 lines, exit 0","runs":"head"},{"item":"AC-7","kind":"bash-test","statement":"the 15 existing check and fetch cases pass unchanged, at base and at head","runs":"base+head"},{"item":"AC-1","kind":"bash-test","statement":"the usage text contains 'list [--file F]'","runs":"head"}]}""";

    private static Dictionary<string, string> Facts() => new(StringComparer.Ordinal)
    {
        ["phasePacket"] = $$"""{"hash":"sha256:aa","dir":"{{Dir}}"}""",
        ["built"] = """{"head":"abc","commits":[]}""",
    };

    private static async Task<BlockStepResult> Run(string acceptance, string signals) =>
        await new ProveIntakeStep(new InMemoryBriefFiles().With("acceptance.json", acceptance).With("signals.json", signals), new InMemoryRepoCheckouts())
            .Run(BuildShipContexts.Of("step.prove.intake", Facts()), Token);

    private static JsonArray Items(BlockStepResult result) =>
        JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:proof")].GetRawText())!["items"]!.AsArray();

    [Fact]
    public async Task Intake_groups_the_lines_flat_signals_list_by_item()
    {
        var items = Items(await Run(Acceptance, Signals));

        Assert.Equal(["AC-1", "AC-7"], items.Select(static i => (string?)i!["id"]), StringComparer.Ordinal);
        Assert.Equal(["list on the repo's binaries.lock prints exactly 2 lines, exit 0", "the usage text contains 'list [--file F]'"], items[0]!["signals"]!.AsArray().Select(static s => (string?)s!["statement"]), StringComparer.Ordinal);
        Assert.Equal(["the 15 existing check and fetch cases pass unchanged, at base and at head"], items[1]!["signals"]!.AsArray().Select(static s => (string?)s!["statement"]), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Intake_gives_each_signal_an_id_and_the_runsAt_key_the_later_prove_steps_read()
    {
        var items = Items(await Run(Acceptance, Signals));

        Assert.Equal(["AC-1-s1", "AC-1-s2"], items[0]!["signals"]!.AsArray().Select(static s => (string?)s!["id"]), StringComparer.Ordinal);
        Assert.Equal(["head", "head"], items[0]!["signals"]!.AsArray().Select(static s => (string?)s!["runsAt"]), StringComparer.Ordinal);
        Assert.Equal("base+head", (string?)items[1]!["signals"]![0]!["runsAt"]);
    }

    [Theory]
    [InlineData("""{"schemaVersion":1,"signals":{"AC-1":[]}}""")]   // the old map shape: no line writes it
    [InlineData("""[{"item":"AC-1"}]""")]
    [InlineData("""{"schemaVersion":1,"signals":[{"kind":"bash-test"}]}""")]
    [InlineData("null")]
    public async Task Intake_with_signals_of_another_shape_fails_the_Step_naming_the_file_and_the_shape(string signals)
    {
        var result = await Run(Acceptance, signals);

        Assert.Null(result.Result);
        Assert.Contains("signals.json", result.Failure, StringComparison.Ordinal);
        Assert.Contains("\"signals\": [", result.Failure, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("""[{"id":"AC-1"}]""")]
    [InlineData("""{"schemaVersion":1,"items":{"AC-1":{}}}""")]
    public async Task Intake_with_an_acceptance_file_of_another_shape_fails_the_Step_naming_the_file(string acceptance)
    {
        var result = await Run(acceptance, Signals);

        Assert.Null(result.Result);
        Assert.Contains("acceptance.json", result.Failure, StringComparison.Ordinal);
    }
}
