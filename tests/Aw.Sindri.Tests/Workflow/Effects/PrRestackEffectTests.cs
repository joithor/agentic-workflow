using System.Text;
using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing.Forge;
using Aw.Sindri.Testing.Scoping;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.restack' (PB spec §4.6): when the pull request's base merged or moved, it rebases the workspace onto the new base without the base's own commits, scans the rebased range
/// for secrets, moves the pull request to the new base and pushes with a lease. It replies 'pr.restacked' with 'restacked', 'nothing', 'conflict' or 'refused' (a scan hit by file and line, a
/// lost lease, or a workspace that is not at the pull request's head); a conflict or a refusal pushes and retargets nothing and leaves the workspace where it was.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrRestackEffectTests
{
    private const string Workspace = "/home/wt/phase-2";
    private const string Instance = "T-4/pr/1";
    private const string OldHead = "1111111111111111111111111111111111111111";
    private const string OldBaseTip = "2222222222222222222222222222222222222222";
    private const string MainTip = "3333333333333333333333333333333333333333";
    private const string NewHead = "4444444444444444444444444444444444444444";
    private static readonly PullRepo _repo = new("github.com", "acme", "web");

    private readonly FakePullRequests _pulls = new(["github.com"]);
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryRunArea _area = new();
    private readonly FakeRunDiff _diff = new();
    private readonly FakeWorkspaceRebase _rebase = new FakeWorkspaceRebase().Tip("main", MainTip).Tip("feat/a", OldBaseTip);
    private readonly FakeDiffContent _content = new();
    private readonly FakeTimeProvider _clock = new(KnownInstants.Epoch);

    public PrRestackEffectTests()
    {
        _diff.Add(Workspace, OldHead);
        _ = _content.Add(Workspace);
        _rebase.RebaseAnswer = new WorkspaceRebased(NewHead);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_pull_request_whose_base_merged_is_rebased_onto_the_default_branch_retargeted_and_pushed_with_a_lease()
    {
        var link = Stack();

        var done = await Restack(link);

        Assert.Equal("restacked", done.Reply?.Value);
        Assert.Equal("main", Assert.Single(_rebase.Fetched));
        Assert.Equal((MainTip, null), Assert.Single(_rebase.Rebases));
        Assert.Equal("main", _pulls.Pulls.Single(p => p.Link == link).Base);
        Assert.Equal(new PullPush(link, Workspace, NewHead, OldHead), Assert.Single(_pulls.Pushes));
        Assert.Empty(_rebase.Resets);
    }

    [Fact]
    public async Task A_stack_parent_that_is_still_open_makes_its_own_branch_the_new_base_and_nothing_is_retargeted()
    {
        var link = Stack();
        _ = _pulls.Seed(new OpenPull(_repo, "feat/a", "main", OldBaseTip, "Parent", "body", Draft: false, Marker: null));

        var done = await Restack(link);

        Assert.Equal("restacked", done.Reply?.Value);
        Assert.Equal("feat/a", Assert.Single(_rebase.Fetched));
        Assert.Equal(OldBaseTip, Assert.Single(_rebase.Rebases).Onto);
        Assert.DoesNotContain(_pulls.Calls, c => string.Equals(c, "Retarget", StringComparison.Ordinal));
    }

    [Fact]
    public async Task The_old_bases_tip_from_the_snapshot_is_where_the_branchs_own_commits_start_so_a_squash_merged_base_is_dropped()
    {
        var link = Stack();

        _ = await Restack(link, prState: $$$"""{"base":{"ref":"feat/a","sha":"{{{OldBaseTip}}}"}}""");

        Assert.Equal((MainTip, OldBaseTip), Assert.Single(_rebase.Rebases));
    }

    [Fact]
    public async Task An_explicit_from_argument_wins_over_the_snapshot()
    {
        var link = Stack();

        _ = await Restack(link, prState: $$$"""{"base":{"sha":"{{{OldBaseTip}}}"}}""", from: NewHead);

        Assert.Equal((MainTip, NewHead), Assert.Single(_rebase.Rebases));
    }

    [Fact]
    public async Task A_pull_request_already_on_its_new_base_replies_nothing_and_touches_nothing()
    {
        var link = Stack(baseBranch: "main");
        _ = _rebase.Descends(MainTip, OldHead);

        var done = await Restack(link);

        Assert.Equal("nothing", done.Reply?.Value);
        Assert.Empty(_rebase.Rebases);
        Assert.Empty(_pulls.Pushes);
    }

    [Fact]
    public async Task A_head_that_already_has_the_new_base_is_only_retargeted()
    {
        var link = Stack();
        _ = _rebase.Descends(MainTip, OldHead);

        var done = await Restack(link);

        Assert.Equal("restacked", done.Reply?.Value);
        Assert.Equal("main", _pulls.Pulls.Single(p => p.Link == link).Base);
        Assert.Empty(_rebase.Rebases);
        Assert.Empty(_pulls.Pushes);
    }

    [Fact]
    public async Task A_conflict_replies_conflict_names_the_paths_and_pushes_and_retargets_nothing()
    {
        var link = Stack();
        _rebase.RebaseAnswer = new WorkspaceRebased(["src/a.cs", "src/b.cs"]);

        var done = await Restack(link);

        Assert.Equal("conflict", done.Reply?.Value);
        Assert.Contains("src/a.cs", Content(done), StringComparison.Ordinal);
        Assert.Empty(_pulls.Pushes);
        Assert.Equal("feat/a", _pulls.Pulls.Single(p => p.Link == link).Base);
    }

    [Fact]
    public async Task A_secret_on_a_rebased_line_refuses_by_file_and_line_never_the_match_and_puts_the_workspace_back()
    {
        var link = Stack();
        _ = _content.Adding(Workspace, "src/a.cs", "var ok = 1;", "var key = \"SECRET-token\";");

        var done = await Restack(link);

        Assert.Equal("refused", done.Reply?.Value);
        var fact = Content(done);
        Assert.Contains("src/a.cs:2", fact, StringComparison.Ordinal);
        Assert.DoesNotContain("SECRET-token", fact, StringComparison.Ordinal);
        Assert.Equal(OldHead, Assert.Single(_rebase.Resets));
        Assert.Empty(_pulls.Pushes);
        Assert.Equal("feat/a", _pulls.Pulls.Single(p => p.Link == link).Base);
    }

    [Fact]
    public async Task A_workspace_that_is_not_at_the_pull_requests_head_refuses_and_rebases_nothing()
    {
        var link = Stack();
        _diff.Add(Workspace, "9999999999999999999999999999999999999999");

        var done = await Restack(link);

        Assert.Equal("refused", done.Reply?.Value);
        Assert.Empty(_rebase.Rebases);
    }

    [Fact]
    public async Task A_lease_the_forge_turns_down_refuses_and_puts_the_workspace_back()
    {
        var link = Stack();
        _pulls.PushAnswer = PullPushOutcome.HeadMoved;

        var done = await Restack(link);

        Assert.Equal("refused", done.Reply?.Value);
        Assert.Equal(OldHead, Assert.Single(_rebase.Resets));
    }

    [Fact]
    public async Task A_closed_pull_request_replies_nothing()
    {
        var link = Stack();
        _pulls.Pulls.Single(p => p.Link == link).State = PullState.Closed;

        var done = await Restack(link);

        Assert.Equal("nothing", done.Reply?.Value);
        Assert.Empty(_rebase.Fetched);
    }

    [Fact]
    public async Task Restack_stores_fact_restack_and_appends_the_restack_row_once()
    {
        var link = Stack();

        var done = await Restack(link);
        _ = await Restack(link);

        var fact = JsonDocument.Parse(Content(done)).RootElement;
        Assert.Equal(("restacked", "feat/a", "main", NewHead), (fact.GetProperty("outcome").GetString(), fact.GetProperty("oldBase").GetString(), fact.GetProperty("newBase").GetString(), fact.GetProperty("head").GetString()));
        var row = JsonDocument.Parse(Assert.Single(await Rows())).RootElement;
        Assert.Equal(("restack", "feat/a", "main", "restacked"), (row.GetProperty("kind").GetString(), row.GetProperty("oldBase").GetString(), row.GetProperty("newBase").GetString(), row.GetProperty("outcome").GetString()));
    }

    [Fact]
    public async Task A_forge_that_cannot_be_reached_fails_the_effect()
    {
        var link = Stack();
        _pulls.Failure = SindriErrors.ForgeUnavailable.With("offline");

        var result = await Effect().Dispatch(await Context(link, workspace: Workspace), Token);

        Assert.True(result.IsError);
        Assert.Empty(await Rows());
    }

    [Fact]
    public async Task A_restack_with_no_workspace_fails_and_fetches_nothing()
    {
        var link = Stack();

        var result = await Effect().Dispatch(await Context(link, workspace: null), Token);

        Assert.True(result.IsError);
        Assert.Empty(_rebase.Fetched);
    }

    private string Stack(string baseBranch = "feat/a") =>
        _pulls.Seed(new OpenPull(_repo, "feat/b", baseBranch, OldHead, "Child", "body", Draft: false, Marker: null)).Link;

    private PrRestackEffect Effect() => new(_pulls, _diff, _rebase, new RebasedRangeScan(_content, new Redacting()), _facts, _facts, new MergeRecord(_area, _clock));

    private async Task<EffectDone> Restack(string link, string? prState = null, string? from = null)
    {
        var result = await Effect().Dispatch(await Context(link, Workspace, prState, from), Token);
        Assert.False(result.IsError, result.IsError ? result.Error.Message : null);
        return result.Value;
    }

    private async Task<EffectContext> Context(string link, string? workspace, string? prState = null, string? from = null)
    {
        var arguments = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            ["pr"] = await Stored($$$"""{"link":"{{{link}}}","head":{"ref":"feat/b","sha":"{{{OldHead}}}"}}"""),
        };
        if (from is not null)
        {
            arguments["from"] = JsonDocument.Parse(JsonSerializer.Serialize(from)).RootElement.Clone();
        }

        var facts = new Dictionary<FactKey, JsonElement>();
        if (workspace is not null)
        {
            facts[FactKey.From("fact:workspace")] = await Stored(workspace);
        }

        if (prState is not null)
        {
            facts[FactKey.From("fact:prState")] = await Stored(prState);
        }

        return new EffectContext(BlockId.From("effect.pr.restack"), Instance + ":3:0", "cmd-1", FromEngine: false, Instance, "T-4", Attempt: 1, arguments, facts, Text: null);
    }

    private string Content(EffectDone done) => Encoding.UTF8.GetString(_facts.Stored[done.Facts![FactKey.From("fact:restack")].Sha256]);

    private async Task<string[]> Rows() => (await _area.Read(Instance, "babysit.jsonl", Token)).Value.Split('\n', StringSplitOptions.RemoveEmptyEntries);

    private async Task<JsonElement> Stored(string content)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(content), Token);
        return JsonDocument.Parse($$"""{"ref":"{{put.Value.Ref}}","sha256":"{{put.Value.Sha256}}"}""").RootElement.Clone();
    }

    private sealed class Redacting : ITextScrubber
    {
        public string Scrub(string text) => text.Replace("SECRET-token", "[redacted]", StringComparison.Ordinal);
    }
}
