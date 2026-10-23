using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.create-stacked' and 'effect.pr.merge-docs' (PB spec §4.6, §7.2, BD9): the first pushes the docs branch with a lease and opens (or updates) the docs pull request on the
/// source's head branch, refusing a branch someone else moved; the second merges that pull request once the source merged, and leaves it open (pending) when its checks aren't green.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrDocsEffectTests
{
    private const string Workspace = "/home/wt/docs-1";
    private const string Instance = "T-4/docs/1";
    private const string SourceHead = "sindri/l1/p1";
    private const string SourceSha = "1111111111111111111111111111111111111111";
    private const string DocsSha = "2222222222222222222222222222222222222222";
    private const string SourceLink = "https://github.com/acme/web/pull/7";
    private const string Marker = "sindri.docs:acme-web-7";
    private static readonly PullRepo _repo = new("github.com", "acme", "web");

    private readonly FakePullRequests _pulls = new(["github.com"]);
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeRunDiff _diff = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    public PrDocsEffectTests()
    {
        _diff.Add(Workspace, DocsSha);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_stacked_pushes_the_docs_branch_without_a_lease_and_opens_the_pull_request_on_the_source_head()
    {
        var done = await CreateStacked();

        Assert.Equal("opened", done.Reply?.Value);
        Assert.Equal(new PullBranchPush(_repo, Workspace, "docs/" + SourceHead, DocsSha, LeaseSha: null), Assert.Single(_pulls.Pushed));
        var open = Assert.Single(_pulls.Opened, o => o.Head == "docs/" + SourceHead);
        Assert.Equal((SourceHead, DocsSha, "docs: sync", Marker, false), (open.Base, open.HeadSha, open.Title, open.Marker, open.Draft));
        Assert.Contains(SourceLink, open.Body, StringComparison.Ordinal);
        Assert.Contains(SourceSha, open.Body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_stacked_stores_the_docs_pull_request_as_fact_docs_pr()
    {
        var done = await CreateStacked();

        var pr = JsonDocument.Parse(Content(done, "fact:docsPr")).RootElement;
        Assert.Equal((SourceHead, "docs/" + SourceHead, DocsSha, "open"), (pr.GetProperty("base").GetProperty("ref").GetString(), pr.GetProperty("head").GetProperty("ref").GetString(), pr.GetProperty("head").GetProperty("sha").GetString(), pr.GetProperty("state").GetString()));
        Assert.StartsWith("https://github.com/acme/web/pull/", pr.GetProperty("link").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_stacked_updates_the_open_docs_pull_request_and_leases_on_the_sha_it_last_pushed()
    {
        const string Pushed = "3333333333333333333333333333333333333333";
        var seeded = _pulls.Seed(new OpenPull(_repo, "docs/" + SourceHead, SourceHead, Pushed, "Old", "Old body", Draft: false, Marker));

        var done = await CreateStacked(held: $$$"""{"head":{"sha":"{{{Pushed}}}"}}""");

        Assert.Equal("updated", done.Reply?.Value);
        Assert.Equal(new PullBranchPush(_repo, Workspace, "docs/" + SourceHead, DocsSha, Pushed), Assert.Single(_pulls.Pushed));
        Assert.DoesNotContain(_pulls.Opened, o => o.Title == "docs: sync");
        var (link, title, body) = Assert.Single(_pulls.Updated);
        Assert.Equal((seeded.Link, "docs: sync"), (link, title));
        Assert.EndsWith($"<!-- aw-marker:{Marker} -->", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Create_stacked_refuses_a_branch_someone_else_moved()
    {
        _ = _pulls.Seed(new OpenPull(_repo, "docs/" + SourceHead, SourceHead, "4444444444444444444444444444444444444444", "Old", "Old body", Draft: false, Marker));

        var done = await CreateStacked(held: """{"head":{"sha":"3333333333333333333333333333333333333333"}}""");

        Assert.Equal("rejected", done.Reply?.Value);
        Assert.Null(done.Facts);
        Assert.Empty(_pulls.Updated);
    }

    [Fact]
    public async Task Create_stacked_refuses_a_docs_branch_that_exists_with_no_pull_request_of_ours()
    {
        _ = _pulls.Seed(new OpenPull(_repo, "docs/" + SourceHead, SourceHead, "4444444444444444444444444444444444444444", "Theirs", "Theirs", Draft: false, Marker: "someone-else"));

        var done = await CreateStacked();

        Assert.Equal("rejected", done.Reply?.Value);
        Assert.Empty(_pulls.Updated);
    }

    [Fact]
    public async Task Create_stacked_replies_rejected_when_the_remote_refuses_the_push()
    {
        _pulls.PushOutcome = PullPushOutcome.Rejected;

        var done = await CreateStacked();

        Assert.Equal("rejected", done.Reply?.Value);
        Assert.DoesNotContain(_pulls.Opened, o => o.Head == "docs/" + SourceHead);
    }

    [Fact]
    public async Task Create_stacked_replies_rejected_when_the_forge_refuses_the_call()
    {
        _pulls.Failure = SindriErrors.ForgeRefused.With("host not allowed");

        var done = await CreateStacked();

        Assert.Equal("rejected", done.Reply?.Value);
    }

    [Fact]
    public async Task Create_stacked_fails_when_the_forge_cannot_be_reached()
    {
        _pulls.Failure = SindriErrors.ForgeUnavailable.With("offline");

        var result = await new PrCreateStackedEffect(_pulls, Repos(), _diff, _facts, _facts).Dispatch(await StackedContext(held: null), Token);

        Assert.Equal(SindriErrors.ForgeUnavailable.Code, result.Error.Code);
    }

    [Fact]
    public async Task Create_stacked_without_the_source_head_branch_fails_and_pushes_nothing()
    {
        var context = await StackedContext(held: null, pr: SourceLink);

        var result = await new PrCreateStackedEffect(_pulls, Repos(), _diff, _facts, _facts).Dispatch(context, Token);

        Assert.True(result.IsError);
        Assert.Empty(_pulls.Pushed);
    }

    [Fact]
    public async Task Merge_docs_retargets_to_the_default_branch_and_merges_with_the_expected_head()
    {
        var docs = SeedDocs();

        var done = await MergeDocs();

        Assert.Equal("merged", done.Reply?.Value);
        Assert.Equal("main", docs.Base);
        Assert.Equal(PullState.Merged, docs.State);
        string[] calls = ["Find", "Get", "DefaultBranch", "Retarget", "Merge"];
        Assert.Equal(calls, [.. _pulls.Calls.Skip(_pulls.Calls.IndexOf("Find"))], StringComparer.Ordinal);
    }

    [Fact]
    public async Task Merge_docs_stores_fact_docs_merge_and_appends_the_docs_merge_row_once()
    {
        var docs = SeedDocs();

        var done = await MergeDocs();
        _ = await MergeDocs();

        var fact = JsonDocument.Parse(Content(done, "fact:docsMerge")).RootElement;
        Assert.Equal((docs.Link, "merged"), (fact.GetProperty("docsPr").GetString(), fact.GetProperty("outcome").GetString()));
        var row = JsonDocument.Parse(Assert.Single(await Rows(), r => r.Contains("docsMerge", StringComparison.Ordinal))).RootElement;
        Assert.Equal(("docsMerge", docs.Link, "merged", DocsSha), (row.GetProperty("kind").GetString(), row.GetProperty("docsPr").GetString(), row.GetProperty("outcome").GetString(), row.GetProperty("headSha").GetString()));
    }

    [Fact]
    public async Task Merge_docs_uses_the_method_the_recipe_names()
    {
        _ = SeedDocs();
        var context = await MergeContext(("method", "\"rebase\""));

        _ = await MergeEffect().Dispatch(context, Token);

        Assert.Equal(PullMergeMethod.Rebase, Assert.Single(_pulls.Merges).Method);
    }

    [Fact]
    public async Task Merge_docs_stays_open_and_pending_when_its_checks_fail()
    {
        var docs = SeedDocs();
        docs.FailingChecks.Add("build");

        var done = await MergeDocs();

        Assert.Equal("pending", done.Reply?.Value);
        Assert.Equal(PullState.Open, docs.State);
        Assert.Equal(SourceHead, docs.Base);   // not retargeted either
        Assert.Empty(_pulls.Merges);
        Assert.Contains("\"outcome\":\"pending\"", Assert.Single(await Rows(), r => r.Contains("docsMerge", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_docs_stays_pending_while_its_checks_run_and_when_it_conflicts()
    {
        var docs = SeedDocs();
        docs.ChecksState = PullChecksState.Pending;

        var running = await MergeDocs();
        docs.ChecksState = PullChecksState.Passing;
        docs.Mergeable = false;
        var conflicting = await MergeDocs();

        Assert.Equal(("pending", "pending"), (running.Reply?.Value, conflicting.Reply?.Value));
        Assert.Empty(_pulls.Merges);
    }

    [Fact]
    public async Task Merge_docs_is_pending_when_the_forge_blocks_the_merge_or_the_head_moved()
    {
        _ = SeedDocs();
        _pulls.MergeStatus = PullMergeStatus.HeadMoved;

        var done = await MergeDocs();

        Assert.Equal("pending", done.Reply?.Value);
    }

    [Fact]
    public async Task Merge_docs_replies_none_when_there_is_no_docs_pull_request()
    {
        var done = await MergeDocs();

        Assert.Equal("none", done.Reply?.Value);
        Assert.Empty(_pulls.Merges);
        Assert.Contains("\"outcome\":\"none\"", Assert.Single(await Rows(), r => r.Contains("docsMerge", StringComparison.Ordinal)), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Merge_docs_replies_none_for_a_docs_pull_request_closed_by_hand()
    {
        SeedDocs().State = PullState.Closed;

        var done = await MergeDocs();

        Assert.Equal("none", done.Reply?.Value);
    }

    [Fact]
    public async Task Merge_docs_answers_the_forges_error_and_writes_no_row()
    {
        _pulls.Failure = SindriErrors.ForgeUnavailable.With("offline");

        var done = await MergeEffect().Dispatch(await MergeContext(), Token);

        Assert.True(done.IsError);
        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task Merge_docs_records_the_trigger_and_actor_a_recipe_passes()
    {
        _ = SeedDocs();
        var context = await MergeContext(("actor", "\"owner\""), ("trigger", "\"command.merge\""));

        _ = await MergeEffect().Dispatch(context, Token);

        var row = JsonDocument.Parse(Assert.Single(await Rows(), r => r.Contains("docsMerge", StringComparison.Ordinal))).RootElement;
        Assert.Equal(("owner", "command.merge"), (row.GetProperty("actor").GetString(), row.GetProperty("trigger").GetString()));
    }

    private FakePull SeedDocs() => _pulls.Add(_repo, "docs/" + SourceHead, DocsSha) is var pull ? Prepare(pull) : throw new InvalidOperationException();

    private static FakePull Prepare(FakePull pull)
    {
        pull.Marker = Marker;
        pull.Base = SourceHead;
        return pull;
    }

    private static FakeForgeRepos Repos() => new FakeForgeRepos().Put(Workspace, _repo);

    private async Task<EffectDone> CreateStacked(string? held = null)
    {
        var result = await new PrCreateStackedEffect(_pulls, Repos(), _diff, _facts, _facts).Dispatch(await StackedContext(held), Token);
        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private async Task<EffectDone> MergeDocs()
    {
        var result = await MergeEffect().Dispatch(await MergeContext(), Token);
        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private PrMergeDocsEffect MergeEffect() => new(_pulls, _facts, _facts, _profiles, new MergeRecord(_area, _clock));

    private async Task<EffectContext> StackedContext(string? held, string? pr = null)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["pr"] = await Stored(pr ?? $$$"""{"link":"{{{SourceLink}}}","head":{"ref":"{{{SourceHead}}}","sha":"{{{SourceSha}}}"}}"""),
            ["on"] = await Stored($$$"""{"head":{"sha":"{{{SourceSha}}}"}}"""),
            ["workspace"] = await Stored(Workspace),
            ["title"] = Literal("docs: sync"),
        };
        var facts = new Dictionary<FactKey, JsonElement>();
        if (held is not null)
        {
            facts[FactKey.From("fact:docsPr")] = await Stored(held);
        }

        return new EffectContext(BlockId.From("effect.pr.create-stacked"), Instance + ":3:0", "cmd-1", FromEngine: false, Instance, "T-4", Attempt: 1, arguments, facts, Text: null);
    }

    private async Task<EffectContext> MergeContext(params (string Name, string Literal)[] extra)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["pr"] = await Stored($$$"""{"link":"{{{SourceLink}}}","head":{"ref":"{{{SourceHead}}}","sha":"{{{SourceSha}}}"}}"""),
        };
        foreach (var (name, literal) in extra)
        {
            arguments[name] = Literal(literal.Trim('"'));
        }

        return new EffectContext(BlockId.From("effect.pr.merge-docs"), Instance + ":3:0", "cmd-1", FromEngine: false, Instance, "T-4", Attempt: 1, arguments, new Dictionary<FactKey, JsonElement>(), Text: null);
    }

    private string Content(EffectDone done, string key) => Encoding.UTF8.GetString(_facts.Stored[done.Facts![FactKey.From(key)].Sha256]);

    private async Task<string[]> Rows() => (await _area.Read(Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private static JsonElement Literal(string text) => JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();

    private async Task<JsonElement> Stored(string content)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(content), Token);
        return JsonDocument.Parse($$"""{"ref":"{{put.Value.Ref}}","sha256":"{{put.Value.Sha256}}"}""").RootElement.Clone();
    }
}
