using Aw.Kernel;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Host;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.Commands;

/// <summary>How the CLI classes itself and the caller headers it sends (API spec §6 "Who sends what", §3.1 repo).</summary>
public sealed class CliCallerTests
{
    private static FakeProcessAncestry Shell => FakeProcessAncestry.Under(Environment.ProcessId, "zsh", "login");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    [Theory, Trait("Lane", "unit")]
    [InlineData(null, true, true, null)]
    [InlineData(null, false, false, "no terminal")]
    [InlineData("CLAUDECODE", true, false, "CLAUDECODE is set")]
    [InlineData("AW_SINDRI_CHILD", true, false, "AW_SINDRI_CHILD is set")]
    [InlineData("CODEX_SANDBOX", true, false, "CODEX_SANDBOX is set")]
    [InlineData("CURSOR_AGENT", true, false, "CURSOR_AGENT is set")]
    [InlineData("CLAUDECODE", false, false, "CLAUDECODE is set")]
    public void Cli_is_human_only_at_a_terminal_with_no_agent_signal(string? variable, bool terminal, bool human, string? reason)
    {
        var session = AgentSession.Detect(n => n == variable ? "1" : null, Shell, terminal);

        Assert.Equal((human, reason), (session.Human, session.SessionReason));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("claude", "claude")]
    [InlineData("codex", "codex")]
    [InlineData("cursor-agent", "cursor-agent")]
    [InlineData("/opt/homebrew/bin/codex", "codex")]
    public void A_harness_parent_makes_the_cli_a_session(string parent, string named)
    {
        var session = AgentSession.Detect(_ => null, FakeProcessAncestry.Under(Environment.ProcessId, "zsh", parent, "login"), interactive: true);

        Assert.Equal((false, true, $"parent process {named}"), (session.Human, session.InAgent, session.SessionReason));
    }

    [Fact, Trait("Lane", "unit")]
    public void Prover_marker_skips_the_parent_check_but_not_the_variables()
    {
        var underClaude = FakeProcessAncestry.Under(Environment.ProcessId, "zsh", "claude");

        var prover = AgentSession.Detect(n => n == "AW_SINDRI_PROVER" ? "1" : null, underClaude, interactive: true);
        var withVariable = AgentSession.Detect(n => string.Equals(n, "AW_SINDRI_PROVER", StringComparison.Ordinal) || string.Equals(n, "CLAUDECODE", StringComparison.Ordinal) ? "1" : null, underClaude, interactive: true);

        Assert.Equal((true, true), (prover.Human, prover.Prover));
        Assert.Equal((false, "CLAUDECODE is set"), (withVariable.Human, withVariable.SessionReason));
    }

    [Fact, Trait("Lane", "unit")]
    public void An_ancestry_that_cant_be_read_is_ignored()
    {
        var session = AgentSession.Detect(_ => null, new FakeProcessAncestry(), interactive: true);

        Assert.True(session.Human);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Human_cli_sends_caller_terminal_client_and_token()
    {
        var tokens = new InMemoryHumanTokenStore();
        _ = await tokens.Mint(Token);

        var sent = await Sent(CallerHeaders.For(Human(), tokens, Repo("demo")));

        Assert.Equal(("human", "1", "cli", "token-1", "demo"),
            (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Terminal), Header(sent, SindriHeaders.Client), Header(sent, SindriHeaders.Token), Header(sent, SindriHeaders.Repo)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Prover_marker_labels_the_human_cli_prover()
    {
        var sent = await Sent(CallerHeaders.For(Human() with { Prover = true }, new InMemoryHumanTokenStore(), Repo(name: null)));

        Assert.Equal(("human", "prover"), (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Client)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task The_token_is_read_when_the_call_is_sent_so_a_fresh_start_counts()
    {
        var tokens = new InMemoryHumanTokenStore();
        var headers = CallerHeaders.For(Human(), tokens, Repo(name: null));
        _ = await tokens.Mint(Token);
        _ = await tokens.Mint(Token);   // 'sindri' started the control plane after the headers were made

        var sent = await Sent(headers);

        Assert.Equal("token-2", Header(sent, SindriHeaders.Token));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Session_cli_sends_no_token_and_no_terminal()
    {
        var tokens = new InMemoryHumanTokenStore();
        _ = await tokens.Mint(Token);

        var sent = await Sent(CallerHeaders.For(new AgentSession(Signal: null, Terminal: false, Prover: false), tokens, Repo("demo")));

        Assert.Equal(("session", null, "cli", null, "demo"),
            (Header(sent, SindriHeaders.Caller), Header(sent, SindriHeaders.Terminal), Header(sent, SindriHeaders.Client), Header(sent, SindriHeaders.Token), Header(sent, SindriHeaders.Repo)));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Agent_cli_never_reads_the_token_file()
    {
        var tokens = new CountingTokenStore();

        var sent = await Sent(CallerHeaders.For(new AgentSession(Signal: "CLAUDECODE is set", Terminal: true, Prover: false), tokens, Repo(name: null)));

        Assert.Equal(("session", 0), (Header(sent, SindriHeaders.Caller), tokens.Reads));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Repo_header_comes_from_the_main_checkout_and_the_longest_profile_path()
    {
        var approvals = await Approved(new RepoEntry("work", "/work"), new RepoEntry("demo", "/work/demo"));
        var locator = new FakeRepoLocator().In("/work/demo/.claude/worktrees/x", "/work/demo");

        var sent = await Sent(CallerHeaders.For(Human(), new InMemoryHumanTokenStore(),
            ct => CallerHeaders.RepoOf(approvals, locator, "/work/demo/.claude/worktrees/x", ct)));

        Assert.Equal("demo", Header(sent, SindriHeaders.Repo));
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("/elsewhere", true)]
    [InlineData("/work/demo", false)]
    public async Task Outside_any_profile_repo_the_repo_header_is_omitted(string directory, bool approved)
    {
        var approvals = approved ? await Approved(new RepoEntry("demo", "/work/demo")) : new InMemoryProfiles();
        var locator = new FakeRepoLocator().In("/work/demo", "/work/demo");

        var sent = await Sent(CallerHeaders.For(new AgentSession(Signal: null, Terminal: false, Prover: false), new InMemoryHumanTokenStore(),
            ct => CallerHeaders.RepoOf(approvals, locator, directory, ct)));

        Assert.Null(Header(sent, SindriHeaders.Repo));
    }

    private static AgentSession Human() => new(Signal: null, Terminal: true, Prover: false);

    private static Func<CancellationToken, ValueTask<RepoName?>> Repo(string? name) =>
        _ => ValueTask.FromResult(name is null ? (RepoName?)null : RepoName.From(name));

    private static async Task<InMemoryProfiles> Approved(params RepoEntry[] repos)
    {
        var profiles = new InMemoryProfiles();
        var loaded = SliceHarness.Profile();
        _ = await profiles.Save(loaded with { Profile = loaded.Profile with { Repos = repos } }, Token);
        return profiles;
    }

    private static async Task<HttpRequestMessage> Sent(CallerHeaders headers)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, new Uri("http://127.0.0.1:7410/sindri/status"));
        await headers.Apply(request, Token);
        return request;
    }

    private static string? Header(HttpRequestMessage request, string name) =>
        request.Headers.TryGetValues(name, out var values) ? string.Join(',', values) : null;

    /// <summary>A token store that counts its reads (and has no token).</summary>
    private sealed class CountingTokenStore : IHumanTokenStore
    {
        public int Reads { get; private set; }

        public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken) => throw new NotSupportedException("the CLI never mints");

        public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken)
        {
            Reads++;
            return ValueTask.FromResult<Result<HumanToken?>>((HumanToken?)null);
        }

        public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken) => throw new NotSupportedException("the CLI never removes");
    }
}
