using Aw.Sindri.Infrastructure;
using Aw.Sindri.Tests.Ledger;

namespace Aw.Sindri.Tests.Auth;

/// <summary>The prover confirms what the app confirms only in a scratch home that holds the marker only the prover writes (setup-once spec §4 item 6).</summary>
public sealed class ProverMarkerTests
{
    [Fact, Trait("Lane", "integration")]
    public void A_temp_home_with_the_marker_may_confirm_and_one_without_may_not()
    {
        using var temp = new TempHome();
        using var state = new TempHome();
        var marked = Path.Combine(temp.Path, "marked");
        var unmarked = Path.Combine(temp.Path, "unmarked");
        _ = Directory.CreateDirectory(marked);
        _ = Directory.CreateDirectory(unmarked);
        File.WriteAllText(Path.Combine(marked, ".prover-scratch"), string.Empty);

        Assert.True(Settings(marked, temp, state).ProverMayConfirm);
        Assert.False(Settings(unmarked, temp, state).ProverMayConfirm);
    }

    [Fact, Trait("Lane", "integration")]
    public void The_owners_default_home_never_may_confirm_even_with_the_marker_in_it()
    {
        using var temp = new TempHome();
        using var state = new TempHome();
        var home = Path.Combine(state.Path, "sindri-app");
        _ = Directory.CreateDirectory(home);
        File.WriteAllText(Path.Combine(home, ".prover-scratch"), string.Empty);

        Assert.False(Settings(home, temp, state).ProverMayConfirm);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_marker_is_a_file_in_the_home() =>
        Assert.Equal("/h/.prover-scratch", new SindriSettings("/h", "/h/profile/profile.yaml").ProverScratchPath);

    private static SindriSettings Settings(string home, TempHome temp, TempHome state) =>
        new(home, Path.Combine(home, "profile", "profile.yaml")) { TempDirectory = temp.Path, StateDirectory = state.Path };
}
