using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

[Lane(TestLane.Unit)]
public sealed class UiStatePathsTests
{
    [Fact]
    public void They_sit_in_a_ui_directory_beside_control_json()
    {
        var paths = UiStatePaths.From(name => name == "AW_SINDRI_HOME" ? "/s" : null, "/u");

        Assert.Equal(Path.Combine("/s", "ui"), paths.Directory);
        Assert.Equal(Path.Combine("/s", "ui", "preferences.json"), paths.Preferences);
        Assert.Equal(Path.Combine("/s", "ui", "instance.sock"), paths.InstanceSocket);
        Assert.Equal(Path.Combine("/s", "ui", "instance.lock"), paths.InstanceLock);
    }

    [Fact]
    public void They_follow_the_home_default() =>
        Assert.Equal(Path.Combine("/u", ".agentic-workflow", "sindri-app", "ui"), UiStatePaths.From(_ => null, "/u").Directory);
}
