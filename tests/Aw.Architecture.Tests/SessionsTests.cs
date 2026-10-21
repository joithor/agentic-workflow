using Aw.Architecture.Tests.Rules;
using Aw.Architecture.Tests.TmuxFixtures;

namespace Aw.Architecture.Tests;

/// <summary>ARCH-SESS-001 (session collector spec CI-5): only TmuxCli runs tmux, with five read verbs and -S; TmuxAttachLine builds the attach line as data and nothing executes it.</summary>
[Trait("Lane", "integration")]
public sealed class SessionsTests
{
    private static readonly string[] _layers = ["Core", "Features", "Infrastructure"];

    private static IEnumerable<Type> SessionTypes() =>
        Production.Assemblies.SelectMany(static a => a.GetTypes())
            .Where(static t => _layers.Any(l => (t.Namespace ?? string.Empty).StartsWith($"Aw.Sindri.{l}.Sessions", StringComparison.Ordinal)));

    [Fact]
    public void Only_TmuxCli_runs_tmux_with_five_read_verbs_and_TmuxAttachLine_is_never_executed() =>
        Verify.None(TmuxRules.Check(SessionTypes(), "TmuxCli", "TmuxAttachLine"));

    [Fact]
    public void A_cli_that_names_a_fifth_verb_or_skips_a_socket_is_reported() =>
        Verify.Exactly(TmuxRules.Check([typeof(BadCli), typeof(GoodAttachLine)], nameof(BadCli), nameof(GoodAttachLine)), TmuxRules.Id,
            "BadCli names the tmux verb 'kill-server'", "BadCli never names the verb 'list-clients'", "BadCli never names the verb 'display-message'", "BadCli never names the verb 'capture-pane'", "BadCli never passes -S");

    [Fact]
    public void A_stray_type_that_names_a_verb_or_runs_tmux_is_reported() =>
        Verify.Exactly(TmuxRules.Check([typeof(GoodCli), typeof(GoodAttachLine), typeof(StrayAttach), typeof(StrayRunner)], nameof(GoodCli), nameof(GoodAttachLine)), TmuxRules.Id,
            "StrayAttach names the tmux verb 'attach'", "StrayRunner starts a process and names 'tmux'");

    [Fact]
    public void An_attach_line_that_a_process_runner_takes_is_reported() =>
        Verify.Exactly(TmuxRules.Check([typeof(GoodCli), typeof(GoodAttachLine), typeof(RunsAttachLine)], nameof(GoodCli), nameof(GoodAttachLine)), TmuxRules.Id,
            "RunsAttachLine starts a process and calls GoodAttachLine");

    [Fact]
    public void The_allowed_pair_alone_passes() =>
        Verify.None(TmuxRules.Check([typeof(GoodCli), typeof(GoodAttachLine)], nameof(GoodCli), nameof(GoodAttachLine)));
}
