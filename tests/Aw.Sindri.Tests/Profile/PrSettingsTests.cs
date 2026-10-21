using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.pr' and 'workflow.docs' keys (PR babysitter spec W27): validated, shown in the approval diff, and absent from the canonical JSON when unset.</summary>
public sealed class PrSettingsTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    private static SindriProfile With(PrSettings pr) => _valid with { Workflow = new(Pr: pr) };

    private static SindriProfile With(DocsSettings docs) => _valid with { Workflow = new(Docs: docs) };

    [Fact, Trait("Lane", "unit")]
    public void A_full_pr_block_and_a_docs_block_are_accepted()
    {
        var pr = new PrSettings(Bots: ["dependabot[bot]"], AutofixCap: 5, PollInterval: "60s", RemindAfter: "14d", Replies: "post", MergeMethod: "rebase", HeldBlocksMerge: true, DocsMergeWait: "30m");

        Assert.True(ProfileRules.Check(With(pr)).IsSuccess);
        Assert.True(ProfileRules.Check(With(new DocsSettings(Globs: ["docs/**", "*.md"], Sync: "off"))).IsSuccess);
        Assert.True(ProfileRules.Check(With(new PrSettings())).IsSuccess);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData(0, null, null, null, null, "workflow.pr.autofixCap")]
    [InlineData(6, null, null, null, null, "workflow.pr.autofixCap")]
    [InlineData(null, "1s", null, null, null, "workflow.pr.pollInterval")]
    [InlineData(null, null, "1w", null, null, "workflow.pr.remindAfter")]
    [InlineData(null, null, null, "auto", null, "workflow.pr.replies")]
    [InlineData(null, null, null, null, "fast-forward", "workflow.pr.mergeMethod")]
    public void A_bad_pr_key_is_refused_naming_the_key(int? cap, string? poll, string? remind, string? replies, string? method, string key)
    {
        var error = ProfileRules.Check(With(new PrSettings(AutofixCap: cap, PollInterval: poll, RemindAfter: remind, Replies: replies, MergeMethod: method))).Error;

        Assert.Contains(key, error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_bad_docs_merge_wait_bot_or_docs_key_is_refused()
    {
        Assert.Contains("workflow.pr.docsMergeWait", ProfileRules.Check(With(new PrSettings(DocsMergeWait: "soon"))).Error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.pr.bots[0]", ProfileRules.Check(With(new PrSettings(Bots: [""]))).Error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.docs.sync", ProfileRules.Check(With(new DocsSettings(Sync: "maybe"))).Error.Message, StringComparison.Ordinal);
        Assert.Contains("workflow.docs.globs[0]", ProfileRules.Check(With(new DocsSettings(Globs: [""]))).Error.Message, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_diff_names_each_key_that_changes()
    {
        var live = ProfileRules.Check(_valid).Value;

        Assert.Equal(
            ["workflow.pr.autofixCap", "workflow.pr.bots", "workflow.pr.heldBlocksMerge", "workflow.pr.mergeMethod"],
            ProfileDiff.Between(live, With(new PrSettings(Bots: ["a[bot]"], AutofixCap: 4, MergeMethod: "merge", HeldBlocksMerge: true))));
        Assert.Equal(["workflow.docs.globs", "workflow.docs.sync"], ProfileDiff.Between(live, With(new DocsSettings(Globs: ["docs/**"], Sync: "off"))));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_the_blocks_keeps_its_canonical_json()
    {
        var json = Encoding.UTF8.GetString(AwJson.Serialize(ProfileRules.Check(_valid with { Workflow = new("1m") }).Value, SliceHarness.Json));

        Assert.DoesNotContain("\"pr\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"docs\"", json, StringComparison.Ordinal);
    }
}
