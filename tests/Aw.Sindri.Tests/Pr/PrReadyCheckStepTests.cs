using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Pr;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Tests.Workflow.Blocks.Contracts;

namespace Aw.Sindri.Tests.Pr;

/// <summary>'step.pr.ready-check': one forge read compared with the recorded snapshot; 'same' returns to ready without a notification, 'changed' re-snapshots.</summary>
[Lane(TestLane.Unit)]
public sealed class PrReadyCheckStepTests
{
    private const string Link = "https://forge.test/acme/app/pull/7";
    private const string Head = "aaaa1111";

    private readonly FakePullRequests _pulls = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static PullView Green() => new(Link, PullState.Open, Head, Mergeable: true, ReviewDecision: "APPROVED", UnresolvedThreads: 0, new PullChecks(PullChecksState.Passing, Head, []), MergedSha: null);

    // The snapshot fact: the pull request's fields in camelCase, enums by name.
    private static string Snapshot(PullView pull) => new JsonObject
    {
        ["link"] = pull.Link,
        ["state"] = pull.State.ToString(),
        ["headSha"] = pull.HeadSha,
        ["mergeable"] = pull.Mergeable,
        ["reviewDecision"] = pull.ReviewDecision,
        ["unresolvedThreads"] = pull.UnresolvedThreads,
        ["checks"] = new JsonObject
        {
            ["state"] = pull.Checks.State.ToString(),
            ["sha"] = pull.Checks.Sha,
            ["failing"] = new JsonArray([.. pull.Checks.Failing.Select(static f => (JsonNode)JsonValue.Create(f)!)]),
        },
        ["mergedSha"] = pull.MergedSha,
    }.ToJsonString();

    private Task<BlockStepResult> Run(string? at, string? pr = Link)
    {
        Dictionary<string, string> facts = new(StringComparer.Ordinal);
        if (pr is not null)
        {
            facts["pr"] = $"\"{pr}\"";
        }

        if (at is not null)
        {
            facts["prState"] = at;
        }

        return new PrReadyCheckStep(_pulls).Run(BuildShipContexts.Of("step.pr.ready-check", facts), Token).AsTask();
    }

    [Fact]
    public async Task A_pull_request_that_still_matches_its_snapshot_is_the_same_and_writes_nothing()
    {
        _pulls.Add(Green());

        var result = await Run(Snapshot(Green()));

        Assert.Equal("same", result.Result!.Outcome.Value);
        Assert.Empty(result.Result.Outputs);
        Assert.Equal(1, _pulls.Reads);
    }

    public static TheoryData<string> Changes => new()
    {
        "head", "checksSha", "checksState", "review", "mergeable", "threads", "merged", "closed",
    };

    [Theory]
    [MemberData(nameof(Changes))]
    public async Task Any_change_since_the_snapshot_is_changed(string change)
    {
        var then = Green();
        _pulls.Add(change switch
        {
            "head" => then with { HeadSha = "bbbb2222" },
            "checksSha" => then with { Checks = then.Checks with { Sha = "bbbb2222" } },
            "checksState" => then with { Checks = then.Checks with { State = PullChecksState.Failing, Failing = ["build"] } },
            "review" => then with { ReviewDecision = "CHANGES_REQUESTED" },
            "mergeable" => then with { Mergeable = false },
            "threads" => then with { UnresolvedThreads = 1 },
            "merged" => then with { State = PullState.Merged, MergedSha = "cccc3333" },
            _ => then with { State = PullState.Closed },
        });

        var result = await Run(Snapshot(then));

        Assert.Equal("changed", result.Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_snapshot_that_is_not_a_pull_request_view_counts_as_changed()
    {
        _pulls.Add(Green());

        Assert.Equal("changed", (await Run("{\"unexpected\":true}")).Result!.Outcome.Value);
        Assert.Equal("changed", (await Run(at: null)).Result!.Outcome.Value);
    }

    [Fact]
    public async Task A_missing_pull_request_link_fails_the_Step()
    {
        var result = await Run(Snapshot(Green()), pr: null);

        Assert.Null(result.Result);
        Assert.Contains("fact:pr", result.Failure, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_forge_that_cannot_be_read_fails_the_Step()
    {
        var result = await Run(Snapshot(Green()));   // no pull request added

        Assert.Null(result.Result);
        Assert.Contains(Link, result.Failure, StringComparison.Ordinal);
    }
}
