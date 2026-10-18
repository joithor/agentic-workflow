using Aw.Kernel.Testing;
using Aw.Sindri.Core;
using Aw.Sindri.Infrastructure.Processes;
using Aw.Sindri.Infrastructure.Secrets;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.OnyxAccount;

// Spec §5.3: the secret reaches the store on the tool's standard input, never as an argument; without a store it goes to an owner-only file.
[Lane(TestLane.Unit)]
public sealed class KeychainSecretWriterTests : IDisposable
{
    private const string Key = "on_Abc123-xyz.789";

    private readonly string _home = Path.Combine(Path.GetTempPath(), "aw-secret-" + RealTime.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_home))
        {
            Directory.Delete(_home, recursive: true);
        }
    }

    private static ToolRun Ok() => new(ToolOutcome.Exited, 0, string.Empty);

    private KeychainSecretWriter Writer(FakeToolRunner runner, KeychainPlatform platform) => new(runner, new FakeSindriHome(_home), platform);

    [Fact]
    public void On_macos_the_command_goes_to_security_on_standard_input_and_the_key_is_in_no_argument()
    {
        var runner = new FakeToolRunner(Ok());

        var pointer = Writer(runner, KeychainPlatform.MacOS).Store("onyx", "sindri-onyx", Key).Value;

        Assert.Equal("keychain:onyx/sindri-onyx", pointer.ToString());
        var call = Assert.Single(runner.Calls);
        Assert.Equal("security", call.FileName);
        Assert.Equal(["-i"], call.Arguments);
        Assert.Equal($"add-generic-password -U -s onyx -a sindri-onyx -w {Key}\n", Assert.Single(runner.Inputs));
        Assert.DoesNotContain(call.Arguments, static a => a.Contains(Key, StringComparison.Ordinal));
    }

    [Fact]
    public void On_linux_secret_tool_reads_the_key_from_standard_input()
    {
        var runner = new FakeToolRunner(Ok());

        var pointer = Writer(runner, KeychainPlatform.Linux).Store("onyx", "sindri-onyx", Key).Value;

        Assert.Equal("keychain:onyx/sindri-onyx", pointer.ToString());
        var call = Assert.Single(runner.Calls);
        Assert.Equal("secret-tool", call.FileName);
        Assert.Equal(["store", "--label", "onyx/sindri-onyx", "service", "onyx", "account", "sindri-onyx"], call.Arguments);
        Assert.Equal(Key, Assert.Single(runner.Inputs));
        Assert.DoesNotContain(call.Arguments, static a => a.Contains(Key, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void A_missing_or_failing_store_falls_back_to_an_owner_only_file(bool macos)
    {
        var runner = new FakeToolRunner(ToolRun.Missing);

        var pointer = Writer(runner, macos ? KeychainPlatform.MacOS : KeychainPlatform.Linux).Store("onyx", "sindri-onyx", Key).Value;

        var path = Path.Combine(_home, "secrets", "onyx-sindri-onyx");
        Assert.Equal($"file:{path}", pointer.ToString());
        Assert.Equal(Key, File.ReadAllText(path));
        if (!OperatingSystem.IsWindows())
        {
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, File.GetUnixFileMode(Path.Combine(_home, "secrets")));
        }
    }

    [Fact]
    public void A_nonzero_exit_also_falls_back_to_the_file()
    {
        var runner = new FakeToolRunner(new ToolRun(ToolOutcome.Exited, 1, string.Empty));

        var pointer = Writer(runner, KeychainPlatform.Linux).Store("onyx", "sindri-onyx", Key).Value;

        Assert.StartsWith("file:", pointer.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_secret_the_command_line_could_misread_goes_to_the_file_and_never_to_security()
    {
        var runner = new FakeToolRunner(Ok());

        var pointer = Writer(runner, KeychainPlatform.MacOS).Store("onyx", "sindri-onyx", "has space; and \"quotes\"").Value;

        Assert.StartsWith("file:", pointer.ToString(), StringComparison.Ordinal);
        Assert.Empty(runner.Calls);
    }

    [Fact]
    public void No_store_and_an_unwritable_home_is_sindri_onyx_009_without_the_key()
    {
        var blocker = Path.Combine(_home, "blocker");
        _ = Directory.CreateDirectory(_home);
        File.WriteAllText(blocker, "a file where the home should be");
        var writer = new KeychainSecretWriter(new FakeToolRunner(ToolRun.Missing), new FakeSindriHome(blocker), KeychainPlatform.Linux);

        var result = writer.Store("onyx", "sindri-onyx", Key);

        Assert.Equal(SindriErrors.OnyxSecretNotStored.Code, result.Error.Code);
        Assert.DoesNotContain(Key, result.Error.Message, StringComparison.Ordinal);
    }
}
