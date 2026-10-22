using System.Text.Json;
using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;

namespace Aw.Sindri.Tests.Workflow.Pr;

[Lane(TestLane.Unit)]
public sealed class PrCiLogStepTests
{
    private static readonly PullRepo _repo = new("forge.test", "acme", "widgets");

    private readonly FakePullRequests _pulls = new(["forge.test"]);

    [Fact]
    public async Task Ci_log_keeps_an_excerpt_of_each_failing_check_and_reports_logged()
    {
        var pull = _pulls.Add(_repo, "feature/x", "abc");
        _pulls.CheckLogs["build"] = "error CS0103 in Foo.cs";
        var result = await Run(pull.Link, State("abc", "build", "test"));

        Assert.Equal("logged", result.Result?.Outcome.Value);
        var fact = Fact(result);
        Assert.Equal("abc", (string?)fact["head"]);
        var checks = fact["checks"]!.AsArray();
        Assert.Equal(["build", "test"], checks.Select(static c => (string?)c!["name"]), StringComparer.Ordinal);
        Assert.Equal("error CS0103 in Foo.cs", (string?)checks[0]!["excerpt"]);
        Assert.Equal("log of test", (string?)checks[1]!["excerpt"]);
    }

    [Fact]
    public async Task Ci_log_cuts_a_long_log_to_its_tail()
    {
        var pull = _pulls.Add(_repo, "feature/x", "abc");
        _pulls.CheckLogs["build"] = new string('a', 20_000) + "THE-FAILURE";
        var result = await Run(pull.Link, State("abc", "build"));

        var excerpt = (string)Fact(result)["checks"]![0]!["excerpt"]!;
        Assert.Equal(PrCiLogStep.ExcerptLimit, excerpt.Length);
        Assert.EndsWith("THE-FAILURE", excerpt, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ci_log_reports_none_and_reads_no_log_when_nothing_is_failing()
    {
        var pull = _pulls.Add(_repo, "feature/x", "abc");
        var result = await Run(pull.Link, State("abc"));

        Assert.Equal("none", result.Result?.Outcome.Value);
        Assert.Empty(Fact(result)["checks"]!.AsArray());
        Assert.DoesNotContain(_pulls.Calls, static call => string.Equals(call, "CheckLog", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Ci_log_fails_with_the_forge_code_when_the_forge_refuses()
    {
        var result = await Run("https://forge.test/acme/widgets/pull/99", State("abc", "build"));

        Assert.Null(result.Result);
        Assert.Contains("SND-FORGE-", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ci_log_takes_the_link_from_the_snapshot_when_no_argument_names_it()
    {
        var pull = _pulls.Add(_repo, "feature/x", "abc");
        var state = JsonNode.Parse(State("abc", "build"))!.AsObject();
        state["link"] = pull.Link;
        var result = await new PrCiLogStep(_pulls).Run(new StepContext(
            BlockId.From("step.pr.ci-log"),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:prState")] = JsonSerializer.SerializeToElement(state.ToJsonString()) },
            InjectionSections.Empty), TestContext.Current.CancellationToken);

        Assert.Equal("logged", result.Result?.Outcome.Value);
    }

    [Fact]
    public async Task Ci_log_fails_without_the_pull_request_link_or_the_snapshot()
    {
        var step = new PrCiLogStep(_pulls);
        var noLink = await step.Run(new StepContext(
            BlockId.From("step.pr.ci-log"),
            new Dictionary<string, JsonElement>(StringComparer.Ordinal),
            new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:prState")] = JsonSerializer.SerializeToElement(State("abc", "build")) },
            InjectionSections.Empty), TestContext.Current.CancellationToken);
        var noState = await step.Run(Context("https://forge.test/acme/widgets/pull/1", state: null), TestContext.Current.CancellationToken);

        Assert.Null(noLink.Result);
        Assert.Null(noState.Result);
    }

    private static string State(string head, params string[] failing) =>
        new JsonObject { ["head"] = new JsonObject { ["sha"] = head }, ["checks"] = new JsonObject { ["failing"] = new JsonArray([.. failing.Select(static n => (JsonNode)JsonValue.Create(n))]) } }.ToJsonString();

    private static StepContext Context(string link, string? state) => new(
        BlockId.From("step.pr.ci-log"),
        new Dictionary<string, JsonElement>(StringComparer.Ordinal) { ["pr"] = JsonSerializer.SerializeToElement(link) },
        state is null ? [] : new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:prState")] = JsonSerializer.SerializeToElement(state) },
        InjectionSections.Empty);

    private async Task<BlockStepResult> Run(string link, string state) =>
        await new PrCiLogStep(_pulls).Run(Context(link, state), TestContext.Current.CancellationToken);

    private static JsonObject Fact(BlockStepResult result) =>
        (JsonObject)JsonNode.Parse(result.Result!.Outputs[FactKey.From("fact:ciLog")].GetRawText())!;
}
