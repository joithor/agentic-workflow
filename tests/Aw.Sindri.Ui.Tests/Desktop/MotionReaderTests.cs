using System.Diagnostics;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>The OS readers, read-only: GNOME's over a stand-in gsettings, macOS's against the user default it reflects.</summary>
[Lane(TestLane.Integration)]
public sealed class MotionReaderTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Gsettings(string script)
    {
        var path = Path.Combine(_dir, "gsettings");
        return ExecutableScript.Write(path, script + "\n");
    }

    [Theory]
    [InlineData("echo false", true)]
    [InlineData("echo true", false)]
    [InlineData("echo nonsense", false)]
    [InlineData("echo false; exit 1", false)]
    public void Gnome_reduces_motion_only_when_animations_are_off(string script, bool reduce)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "a shell script stands in for gsettings");

        Assert.Equal(reduce, GnomeMotion.Read(Gsettings(script)));
    }

    [Fact]
    public void Gnome_without_gsettings_reads_as_no_reduction() => Assert.False(GnomeMotion.Read(Path.Combine(_dir, "no-such-gsettings")));

    [Fact]
    public void MacOS_reads_the_accessibility_setting()
    {
        Assert.SkipUnless(OperatingSystem.IsMacOS(), "macOS only");

        Assert.Equal(UserDefault(), OperatingSystem.IsMacOS() && MacMotion.Read());
    }

    // The value NSWorkspace reflects: 'defaults read com.apple.universalaccess reduceMotion' (absent: off).
    private static bool UserDefault()
    {
        using var defaults = Process.Start(new ProcessStartInfo("defaults", ["read", "com.apple.universalaccess", "reduceMotion"]) { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false })!;
        var output = defaults.StandardOutput.ReadToEnd();
        defaults.WaitForExit();
        return defaults.ExitCode == 0 && output.Trim() == "1";
    }
}
