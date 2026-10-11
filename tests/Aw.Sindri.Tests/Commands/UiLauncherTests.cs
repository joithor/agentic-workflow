using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

public sealed class UiLauncherTests
{
    [Fact, Trait("Lane", "unit")]
    public void The_app_is_found_next_to_the_sindri_binary_by_default() =>
        Assert.Equal(Path.Combine("/opt/aw", "sindri-ui"), UiLauncher.Locate(_ => null, "/opt/aw"));

    [Fact, Trait("Lane", "unit")]
    public void AW_SINDRI_UI_names_another_app_binary() =>
        Assert.Equal("/elsewhere/sindri-ui", UiLauncher.Locate(n => n == UiLauncher.Variable ? "/elsewhere/sindri-ui" : null, "/opt/aw"));

    [Fact, Trait("Lane", "unit")]
    public void A_missing_app_says_where_it_looked_and_the_fix()
    {
        var line = UiLauncher.Open("/nonexistent/sindri-ui");

        Assert.Equal("not found at /nonexistent/sindri-ui (fix: publish Aw.Sindri.Ui.Desktop next to sindri, or set AW_SINDRI_UI); use sindri status", line);
    }

    [Fact, Trait("Lane", "integration")]
    public void An_app_that_exists_is_started_detached_and_named_with_its_pid()
    {
        var line = UiLauncher.Open("/usr/bin/true");

        Assert.Matches(@"^opened \(true, pid \d+\)$", line);
    }
}
