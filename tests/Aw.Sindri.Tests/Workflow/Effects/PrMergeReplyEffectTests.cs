using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.Blocks.Effect;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Effects;

/// <summary>
/// 'effect.pr.merge' and 'effect.pr.reply' (PB spec §10.1, W23): the merge carries the snapshot's head as the expected head and maps the
/// forge's answer to 'pr.mergeResult' outcomes; a reply posts the drafted text once, a reaction reacts, and the forms that post nothing post nothing.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class PrMergeReplyEffectTests
{
    private const string Link = "https://forge.example/acme/web/pull/7";

    private readonly RecordingPulls _pulls = new();
    private readonly InMemoryFactStore _facts = new();
    private readonly InMemoryProfiles _profiles = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Merge_carries_the_snapshots_head_and_replies_merged()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string> { ["pr"] = "\"" + Link + "\"", ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(new EffectDone(OutcomeName.From("merged"), Existed: false), done.Value);
        Assert.Equal(new PullMerge(Link, PullMergeMethod.Squash, "deadbeef"), Assert.Single(_pulls.Merges));
    }

    [Fact]
    public async Task Merge_uses_the_method_the_recipe_names()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string> { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });
        context = context with { Arguments = new Dictionary<string, JsonElement>(context.Arguments, StringComparer.Ordinal) { ["method"] = Literal("rebase") } };

        _ = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(PullMergeMethod.Rebase, Assert.Single(_pulls.Merges).Method);
    }

    [Theory]
    [InlineData(PullMergeStatus.Blocked, "blocked")]
    [InlineData(PullMergeStatus.HeadMoved, "headMoved")]
    public async Task Merge_maps_a_refusal_to_its_outcome_and_never_forces(PullMergeStatus status, string outcome)
    {
        _pulls.MergeAnswer = new PullMergeOutcome(status, MergedSha: null, "why");
        var context = await Context("effect.pr.merge", new Dictionary<string, string> { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.Equal(OutcomeName.From(outcome), done.Value.Reply);
        Assert.Single(_pulls.Merges);
    }

    [Fact]
    public async Task Merge_answers_the_forges_error()
    {
        _pulls.MergeAnswer = SindriErrors.ForgeUnavailable.With("down");
        var context = await Context("effect.pr.merge", new Dictionary<string, string> { ["pr"] = Link, ["at"] = """{"headSha":"deadbeef"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.True(done.IsError);
    }

    [Fact]
    public async Task Merge_without_a_head_in_the_snapshot_is_an_error_and_merges_nothing()
    {
        var context = await Context("effect.pr.merge", new Dictionary<string, string> { ["pr"] = Link, ["at"] = """{"state":"open"}""" });

        var done = await new PrMergeEffect(_pulls, _facts, _profiles).Dispatch(context, Token);

        Assert.True(done.IsError);
        Assert.Empty(_pulls.Merges);
    }

    [Fact]
    public async Task A_thread_reply_posts_the_drafted_text_with_a_marker_derived_from_the_identity()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string> { ["form"] = "thread", ["comment"] = """{"id":"c1","threadId":"t9"}""", ["body"] = "\"Done, thanks.\"" });

        var done = await new PrReplyEffect(_pulls, _facts).Dispatch(context, Token);

        Assert.Equal(new EffectDone(Reply: null, Existed: false), done.Value);
        Assert.Equal(new PullReply(Link, "t9", "Done, thanks.", "sindri:T-4-pr-1:3:0"), Assert.Single(_pulls.Replies));
    }

    [Fact]
    public async Task A_reply_the_forge_already_has_reports_existed()
    {
        _pulls.Delivery = PullDelivery.AlreadyDelivered;
        var context = await Context("effect.pr.reply", new Dictionary<string, string> { ["form"] = "thread", ["comment"] = """{"id":"c1","threadId":"t9"}""", ["body"] = "\"Done.\"" });

        var done = await new PrReplyEffect(_pulls, _facts).Dispatch(context, Token);

        Assert.True(done.Value.Existed);
    }

    [Fact]
    public async Task A_reaction_reacts_on_the_comments_thread()
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string> { ["form"] = "reaction", ["comment"] = """{"id":"c1","threadId":"t9"}""" });

        _ = await new PrReplyEffect(_pulls, _facts).Dispatch(context, Token);

        Assert.Equal(new PullReaction(Link, "t9", "+1", "sindri:T-4-pr-1:3:0"), Assert.Single(_pulls.Reactions));
    }

    [Theory]
    [InlineData("fixed")]
    [InlineData("fixedReplies")]
    [InlineData("hold")]
    [InlineData("none")]
    public async Task The_forms_that_post_nothing_post_nothing(string form)
    {
        var context = await Context("effect.pr.reply", new Dictionary<string, string> { ["form"] = form, ["comment"] = """{"id":"c1","threadId":"t9"}""" });

        var done = await new PrReplyEffect(_pulls, _facts).Dispatch(context, Token);

        Assert.True(done.IsSuccess);
        Assert.Empty(_pulls.Replies);
        Assert.Empty(_pulls.Reactions);
    }

    private static JsonElement Literal(string text) => JsonDocument.Parse(JsonSerializer.Serialize(text)).RootElement.Clone();

    /// <summary>A context whose arguments are stored facts (a value that isn't JSON is stored as the text it is), with 'fact:pr' on the instance.</summary>
    private async Task<EffectContext> Context(string effect, Dictionary<string, string> arguments)
    {
        var resolved = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var (name, value) in arguments)
        {
            resolved[name] = name == "form" ? Literal(value) : await Stored(value);
        }

        var facts = new Dictionary<FactKey, JsonElement> { [FactKey.From("fact:pr")] = await Stored(Link) };
        return new EffectContext(BlockId.From(effect), "T-4/pr/1:3:0", "cmd-1", FromEngine: false, "T-4/pr/1", "T-4", Attempt: 1, resolved, facts, Text: null);
    }

    private async Task<JsonElement> Stored(string content)
    {
        var put = await _facts.Put(Encoding.UTF8.GetBytes(content), Token);
        return JsonDocument.Parse($$"""{"ref":"{{put.Value.Ref}}","sha256":"{{put.Value.Sha256}}"}""").RootElement.Clone();
    }
}
