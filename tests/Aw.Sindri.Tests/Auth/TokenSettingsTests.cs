using Aw.Sindri.Infrastructure;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Auth;

public sealed class TokenSettingsTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_token_lives_under_auth_and_the_prover_rule_reads_TMPDIR_and_the_state_root()
    {
        var settings = SettingsFromEnvironment.Read(n => n switch { "AW_SINDRI_HOME" => "/h", "TMPDIR" => "/t", "AW_STATE_DIR" => "/state", _ => null });

        Assert.Equal("/h/auth/human.token", settings.TokenPath);
        Assert.Equal("/t", settings.TempDirectory);
        Assert.Equal("/state", settings.StateDirectory);
    }

    [Fact, Trait("Lane", "integration")]
    public void Prover_label_is_allowed_for_a_temp_home_and_refused_for_the_state_home()
    {
        using var temp = new TempHome();
        using var state = new TempHome();

        Assert.True(Settings(Path.Combine(temp.Path, "home"), temp, state).ProverLabelAllowed);
        Assert.True(Settings(Path.Combine(state.Path, "jthorare-agentic-workflow", "proof", "p1", "home"), temp, state).ProverLabelAllowed);
        Assert.False(Settings(Path.Combine(state.Path, "sindri-app"), temp, state).ProverLabelAllowed);
    }

    [Fact, Trait("Lane", "integration")]
    public void A_home_reached_through_a_link_is_judged_where_it_really_is()
    {
        using var temp = new TempHome();
        using var state = new TempHome();
        _ = Directory.CreateDirectory(Path.Combine(state.Path, "sindri-app"));
        _ = Directory.CreateSymbolicLink(Path.Combine(temp.Path, "into-state"), Path.Combine(state.Path, "sindri-app"));
        _ = Directory.CreateDirectory(Path.Combine(temp.Path, "real"));
        _ = Directory.CreateSymbolicLink(Path.Combine(state.Path, "into-temp"), Path.Combine(temp.Path, "real"));

        Assert.False(Settings(Path.Combine(temp.Path, "into-state"), temp, state).ProverLabelAllowed);
        Assert.True(Settings(Path.Combine(state.Path, "into-temp", "home"), temp, state).ProverLabelAllowed);
    }

    private static SindriSettings Settings(string home, TempHome temp, TempHome state) =>
        new(home, Path.Combine(home, "profile", "profile.yaml")) { TempDirectory = temp.Path, StateDirectory = state.Path };
}
