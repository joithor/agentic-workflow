using Aw.Context.Core;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

public sealed class ContextEvalVerbsTests
{
    private static Task<(int Code, string Out)> Verb(params string[] args) => VerbAs(tokens: null, args);

    private static async Task<(int Code, string Out)> VerbAs(IHumanTokenStore? tokens, params string[] args)
    {
        var (stdout, stderr) = (new StringWriter(), new StringWriter());
        var headers = tokens is null ? null : CallerHeaders.For(new AgentSession(Terminal: true, Prover: false), tokens, static _ => ValueTask.FromResult<Core.Ids.RepoName?>(null));
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json, headers);
        var code = await new ContextEvalVerbs(client, new Output(stdout, stderr, SliceHarness.Json), FakeContextEngine.Instance).Run(args, TestContext.Current.CancellationToken);
        return (code, stdout + stderr.ToString());
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_purge_runs_in_process_and_all_says_the_probe_baselines_are_cleared()
    {
        var (plainCode, plain) = await Verb("purge");
        var (allCode, all) = await Verb("purge", "--all");

        Assert.Equal((0, 0), (plainCode, allCode));
        Assert.DoesNotContain("probe baselines", plain, StringComparison.Ordinal);
        Assert.Contains("the probe baselines are cleared too", all, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_purge_is_refused_to_a_session_with_SND_CONTEXT_020_and_open_to_a_human_at_a_terminal()
    {
        var (sessionCode, refused) = await VerbAs(new InMemoryHumanTokenStore(), "purge", "--all");   // no readable token
        var (humanCode, _) = await VerbAs(InMemoryHumanTokenStore.WithToken(), "purge", "--all");

        Assert.NotEqual(0, sessionCode);
        Assert.Contains(ContextErrors.SessionsDisabled.Code, refused, StringComparison.Ordinal);
        Assert.Equal(0, humanCode);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Eval_cli_map_with_no_control_plane_is_the_not_running_error()
    {
        var (code, map) = await Verb("map");

        Assert.Equal(1, code);   // SND-API-001 is attention, as for every graph verb
        Assert.Contains("SND-API-001", map, StringComparison.Ordinal);
    }
}
