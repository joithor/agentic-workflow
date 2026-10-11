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

    [Fact]
    public void A_socket_path_too_long_for_sun_path_moves_under_the_temp_directory_by_a_hash_of_it()
    {
        var deep = Path.Combine("/h", new string('d', 120), "ui");

        var paths = UiStatePaths.In(deep, "/t");

        Assert.Equal(Path.Combine(deep, "instance.lock"), paths.InstanceLock);
        Assert.StartsWith(Path.Combine("/t", "sindri-ui-"), paths.InstanceSocket, StringComparison.Ordinal);
        Assert.EndsWith("instance.sock", paths.InstanceSocket, StringComparison.Ordinal);
        Assert.Equal(paths.InstanceSocket, UiStatePaths.In(deep, "/t").InstanceSocket);
        Assert.NotEqual(paths.InstanceSocket, UiStatePaths.In(deep + "2", "/t").InstanceSocket, StringComparer.Ordinal);
    }

    [Fact]
    public void A_socket_path_that_fits_stays_in_the_ui_directory() =>
        Assert.Equal(Path.Combine("/h", "ui", "instance.sock"), UiStatePaths.In(Path.Combine("/h", "ui"), "/t").InstanceSocket);
}
