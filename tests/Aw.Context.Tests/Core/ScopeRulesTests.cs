using Aw.Context.Core;
using Aw.Context.Core.Engine;

namespace Aw.Context.Tests.Core;

public sealed class ScopeRulesTests
{
    private static readonly ContextRepo[] _repos = [new("split-panes", "/r/a"), new("other", "/r/b")];

    [Fact, Trait("Lane", "unit")]
    public void A_human_reads_every_mapped_repo()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings { Repos = ["other"] }, _repos, new(ContextCallerKind.Human, Repo: null, ContextClient.Cli));

        Assert.Equal(["other"], scoped.Value.Select(r => r.Name), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_is_refused_with_020_while_sessions_are_off()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings(), _repos, new(ContextCallerKind.Session, "split-panes", ContextClient.Http));

        Assert.Equal(ContextErrors.SessionsDisabled.Code, scoped.Error.Code);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("unknown-repo")]
    [InlineData(null)]
    [InlineData("other")]
    public void A_session_outside_its_mapped_repo_gets_003_the_same_way(string? repo)
    {
        var settings = new ContextSettings { SessionsEnabled = true, Repos = ["split-panes"] };

        var scoped = ScopeRules.Resolve(settings, _repos, new(ContextCallerKind.Session, repo, ContextClient.Mcp));

        Assert.Equal(ContextErrors.UnknownAnchor.Code, scoped.Error.Code);
        Assert.Equal("no context is mapped for this session's repo (X-Sindri-Repo)", scoped.Error.Message);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_session_sees_only_its_own_repo()
    {
        var scoped = ScopeRules.Resolve(new ContextSettings { SessionsEnabled = true }, _repos, new(ContextCallerKind.Session, "split-panes", ContextClient.Http));

        Assert.Equal(["split-panes"], scoped.Value.Select(r => r.Name), StringComparer.Ordinal);
    }
}
