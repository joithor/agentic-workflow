using Aw.Kernel.Testing;
using Aw.Sindri.Host.Commands.SessionWrap;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.SessionWrap;

/// <summary>The parts of 'session wrap' that need no process: its arguments, the environment it sets, and the missing-CLI guard.</summary>
[Lane(TestLane.Unit)]
[System.Runtime.Versioning.UnsupportedOSPlatform("windows")]
public sealed class SessionWrapSupportTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("aw-wrap-bin-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static readonly string[] _command = ["claude", "--session-id", "x"];

    [Fact]
    public void Arguments_name_the_launch_and_the_command()
    {
        var parsed = SessionWrapArguments.Parse(["--launch", A.FirstLaunchId.Value, "--", "claude", "--session-id", "x"]);

        Expect.Equal(A.FirstLaunchId, parsed.Value.Launch);
        Assert.Equal(_command, parsed.Value.Argv, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData("--launch")]
    [InlineData("--launch", "LCH-00000000000000000000000000000001")]
    [InlineData("--launch", "LCH-00000000000000000000000000000001", "--")]
    [InlineData("--launch", "nope", "--", "claude")]
    [InlineData("claude")]
    public void Arguments_without_a_launch_and_a_command_are_refused(params string[] args) => Expect.True(SessionWrapArguments.Parse(args).IsError);

    [Fact]
    public void The_environment_sets_child_and_launch_and_passes_on_what_wrap_was_given()
    {
        var given = new Dictionary<string, string?>(StringComparer.Ordinal) { ["AW_SINDRI_ITEM"] = "ITM-1", ["AW_SINDRI_LAUNCH_SECRET"] = "p", ["AW_SINDRI_HOME"] = "/h" };

        var set = WrapEnvironment.For(A.FirstLaunchId, name => given.GetValueOrDefault(name), "/usr/bin/dotnet");

        Expect.Equal(("1", A.FirstLaunchId.Value, "ITM-1", "p", "/h"), (set["AW_SINDRI_CHILD"], set["AW_SINDRI_LAUNCH"], set["AW_SINDRI_ITEM"], set["AW_SINDRI_LAUNCH_SECRET"], set["AW_SINDRI_HOME"]));
    }

    [Fact]
    public void The_environment_puts_the_directory_of_a_wrap_named_sindri_first_on_the_path()
    {
        var given = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/local/bin:/usr/bin" };

        var set = WrapEnvironment.For(A.FirstLaunchId, name => given.GetValueOrDefault(name), "/opt/aw/sindri-app/sindri");

        Expect.Equal("/opt/aw/sindri-app:/usr/local/bin:/usr/bin", set["PATH"]);
    }

    [Fact]
    public void The_path_has_no_second_copy_of_the_directory_already_first()
    {
        var given = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/opt/aw/sindri-app:/usr/bin" };

        var set = WrapEnvironment.For(A.FirstLaunchId, name => given.GetValueOrDefault(name), "/opt/aw/sindri-app/sindri");

        Expect.Equal("/opt/aw/sindri-app:/usr/bin", set["PATH"]);
    }

    [Fact]
    public void The_path_is_left_alone_when_wrap_is_not_named_sindri()
    {
        var given = new Dictionary<string, string?>(StringComparer.Ordinal) { ["PATH"] = "/usr/bin" };

        var set = WrapEnvironment.For(A.FirstLaunchId, name => given.GetValueOrDefault(name), "/usr/share/dotnet/dotnet");

        Assert.False(set.ContainsKey("PATH"));
    }

    [Fact]
    public void The_environment_leaves_out_what_wrap_was_not_given()
    {
        var set = WrapEnvironment.For(A.FirstLaunchId, static _ => null, "/usr/bin/dotnet");

        Assert.Equal(["AW_SINDRI_CHILD", "AW_SINDRI_LAUNCH"], set.Keys.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact]
    public void A_bin_on_the_path_resolves_and_a_missing_or_non_executable_one_does_not()
    {
        var tool = Path.Combine(_dir, "agent-cli");
        _ = ExecutableScript.Write(tool, string.Empty);
        var plain = Path.Combine(_dir, "not-exec");
        File.WriteAllText(plain, "x");
        File.SetUnixFileMode(plain, UnixFileMode.UserRead | UnixFileMode.UserWrite);

        Expect.Equal((true, false, false, true, false), (BinOnPath.Resolves("agent-cli", "/nonexistent:" + _dir), BinOnPath.Resolves("not-exec", _dir),
            BinOnPath.Resolves("agent-cli", path: null), BinOnPath.Resolves(tool, string.Empty), BinOnPath.Resolves("missing", _dir)));
    }
}
