using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>The words of `context map status`: a session's counts are its repo's scope, and the line says so, so they aren't read as the version's (doctor counts the whole map).</summary>
public sealed class ContextMapTextTests
{
    private static readonly ContextRepoName _repo = ContextRepoName.From("aw-split-panes");

    private static MapStatusView View(ContextRepoName? scopedTo) =>
        new(new MapVersion(1, _repo, Commit: null, KeyVersion: 1, DateTimeOffset.UnixEpoch, MapVersionStatus.Current), new MapCounts(13, 8, 0, 0, 0), Orphaned: 0, Retained: 0, scopedTo);

    [Fact, Trait("Lane", "unit")]
    public void A_scoped_status_says_its_counts_are_the_repos_scope_and_not_the_whole_map()
    {
        var text = ContextMapText.Status(View(_repo));

        Assert.Contains("13 nodes, 8 edges, 0 aliases, 0 decisions, 0 overrides in aw-split-panes's scope (the nodes aw-split-panes implements and the intent linked to them; doctor counts the whole map)", text, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void An_unscoped_status_counts_the_version_without_a_scope_note()
    {
        var text = ContextMapText.Status(View(scopedTo: null));

        Assert.Contains("13 nodes, 8 edges, 0 aliases, 0 decisions, 0 overrides; 0 orphaned overrides", text, StringComparison.Ordinal);
        Assert.DoesNotContain("scope", text, StringComparison.Ordinal);
    }
}
