using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Infrastructure;
using Aw.Sindri.Infrastructure.Deploy;

namespace Aw.Sindri.Tests.Settings;

/// <summary>AW_SINDRI_DEPLOY=pod is the one switch for the deployable image (deployable-context spec A2a); unset is the desktop behaviour.</summary>
public sealed class DeployModeTests
{
    private static SindriSettings Read(string? deploy) =>
        SettingsFromEnvironment.Read(name => name == "AW_SINDRI_DEPLOY" ? deploy : null);

    [Theory, Trait("Lane", "unit")]
    [InlineData(null)]
    [InlineData("")]
    public void Unset_is_the_desktop_mode(string? value) => Assert.Equal(DeployMode.Desktop, Read(value).Deploy);

    [Theory, Trait("Lane", "unit")]
    [InlineData("pod")]
    [InlineData("POD")]
    public void Pod_selects_pod_mode(string value) => Assert.Equal(DeployMode.Pod, Read(value).Deploy);

    [Fact, Trait("Lane", "unit")]
    public void An_unknown_value_stays_the_desktop_mode() => Assert.Equal(DeployMode.Desktop, Read("cluster").Deploy);

    [Fact, Trait("Lane", "unit")]
    public async Task Doctor_echoes_pod_mode_and_says_nothing_on_the_desktop()
    {
        var pod = await new DeployDoctorChecks(Read("pod")).Run(TestContext.Current.CancellationToken);
        var desktop = await new DeployDoctorChecks(Read(null)).Run(TestContext.Current.CancellationToken);

        var check = Assert.Single(pod);
        Assert.Equal((CheckState.Ok, "deploy:mode"), (check.State, check.Name));
        Assert.Empty(desktop);
    }
}
