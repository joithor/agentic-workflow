using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class ControlFilePathTests
{
    private static Func<string, string?> Env(params (string Name, string Value)[] vars) =>
        name => vars.FirstOrDefault(v => v.Name == name).Value;

    [Fact]
    public void AW_SINDRI_HOME_wins()
    {
        var path = ControlFilePath.From(Env(("AW_SINDRI_HOME", "/s"), ("AW_STATE_DIR", "/st")), "/u");

        Assert.Equal(Path.Combine("/s", "control.json"), path);
    }

    [Fact]
    public void Then_the_state_dir() =>
        Assert.Equal(Path.Combine("/st", "sindri-app", "control.json"), ControlFilePath.From(Env(("AW_STATE_DIR", "/st")), "/u"));

    [Fact]
    public void Else_the_home_default() =>
        Assert.Equal(Path.Combine("/u", ".agentic-workflow", "sindri-app", "control.json"), ControlFilePath.From(Env(), "/u"));

    // API spec §6: 'serve' mints <home>/auth/human.token beside control.json; the app reads it from the same home.
    [Fact]
    public void The_human_token_sits_in_auth_beside_control_json() =>
        Assert.Equal(Path.Combine("/s", "auth", "human.token"), ControlFilePath.Token(Env(("AW_SINDRI_HOME", "/s")), "/u"));
}
