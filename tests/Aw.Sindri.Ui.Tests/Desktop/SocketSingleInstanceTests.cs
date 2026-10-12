using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
using Aw.Sindri.Ui.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Ui.Tests.Desktop;

/// <summary>What the socket adapter adds to the contract: stale-socket recovery, owner-only modes, and a listener that checks what it reads.</summary>
[Lane(TestLane.Integration)]
public sealed class SocketSingleInstanceTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("sindri-ui-").FullName;

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private UiStatePaths Paths => UiStatePaths.In(Path.Combine(_dir, "ui"));

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private static UnixFileMode Mode(string path) => OperatingSystem.IsWindows() ? UnixFileMode.None : File.GetUnixFileMode(path);

    private SocketSingleInstance Launch() => new(Paths, new FakeTimeProvider());

    [Fact]
    public async Task A_socket_left_by_a_crashed_instance_is_taken_over()
    {
        // A crashed instance leaves its socket file; nothing listens on it, and binding over it fails until it's removed.
        _ = Directory.CreateDirectory(Paths.Directory);
        await File.WriteAllBytesAsync(Paths.InstanceSocket, [], Token);
        await using var first = Launch();
        await using var second = Launch();

        Assert.True(await first.TryClaim(Token));
        await using var reader = new InstanceReader(first, Token);
        Assert.True(await second.Send(InstanceMessages.Focus, Token));
    }

    [Fact]
    public async Task A_home_too_deep_for_a_socket_path_still_claims_and_hears()
    {
        var deep = UiStatePaths.In(Path.Combine(_dir, new string('d', 90), "ui"));
        var socketDirectory = Path.GetDirectoryName(deep.InstanceSocket)!; // why: SocketIn always joins a directory and a file
        try
        {
            await using var first = new SocketSingleInstance(deep, new FakeTimeProvider());
            await using var second = new SocketSingleInstance(deep, new FakeTimeProvider());

            Assert.True(await first.TryClaim(Token));
            Assert.False(await second.TryClaim(Token));
            await using var reader = new InstanceReader(first, Token);
            Assert.True(await second.Send(InstanceMessages.Focus, Token));
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, Mode(socketDirectory));
        }
        finally
        {
            Directory.Delete(socketDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task The_channel_is_owner_only()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Unix file modes");
        await using var first = Launch();

        Assert.True(await first.TryClaim(Token));

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute, Mode(Paths.Directory));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(Paths.InstanceSocket));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(Paths.InstanceLock));
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, Mode(Paths.InstancePid));
    }

    [Fact]
    public async Task The_holder_records_its_own_process_id_and_removes_it_on_exit()
    {
        var first = Launch();
        await using var second = Launch();
        Assert.True(await first.TryClaim(Token));

        Assert.Equal(Environment.ProcessId, second.HolderProcessId());

        await first.DisposeAsync();
        Assert.False(File.Exists(Paths.InstancePid));
    }

    [Fact]
    public async Task Ending_the_instance_removes_its_socket()
    {
        var first = Launch();
        Assert.True(await first.TryClaim(Token));

        await first.DisposeAsync();

        Assert.False(File.Exists(Paths.InstanceSocket));
    }

    // Review run 3 (code-reviewer-2): ui/ removed while the app runs (a proof's temp home cleaned up, the home reset):
    // ending the instance treats the missing directory as already cleaned up instead of throwing out of Main.
    [Fact]
    public async Task Ending_the_instance_after_its_ui_directory_is_gone_is_quiet()
    {
        var first = Launch();
        Assert.True(await first.TryClaim(Token));
        Directory.Delete(Paths.Directory, recursive: true);

        await first.DisposeAsync();

        Assert.False(Directory.Exists(Paths.Directory));
    }

    // A client that skips the adapter's own checks: the listener refuses what it reads (over the limit, not UTF-8, empty,
    // or a message without its newline) and keeps serving.
    [Theory]
    [InlineData(InstanceMessages.MaxLength + 1, (byte)'x', true)]
    [InlineData(4, (byte)0xff, true)]
    [InlineData(0, (byte)'x', true)]
    [InlineData(5, (byte)'x', false)]
    public async Task The_listener_refuses_a_message_over_the_limit_or_not_utf8_or_empty_or_unterminated(int length, byte fill, bool terminated)
    {
        await using var first = Launch();
        await using var second = Launch();
        Assert.True(await first.TryClaim(Token));
        await using var reader = new InstanceReader(first, Token);

        Assert.Equal(0, await RawSend([.. Enumerable.Repeat(fill, length), .. terminated ? "\n"u8.ToArray() : []]));
        Assert.True(await second.Send(InstanceMessages.Focus, Token));

        Assert.Equal([InstanceMessages.Focus], await reader.Take(1));
    }

    private async Task<int> RawSend(byte[] bytes)
    {
        using var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
        await socket.ConnectAsync(new UnixDomainSocketEndPoint(Paths.InstanceSocket), Token);
        _ = await socket.SendAsync(bytes, SocketFlags.None, Token);
        socket.Shutdown(SocketShutdown.Send);
        var ack = new byte[1];
        return await socket.ReceiveAsync(ack, SocketFlags.None, Token) == 1 ? ack[0] : -1;
    }

    // The prover's case: 'ui' is a plain file, so the instance directory can't be made. The launch ends with SND-UI-004
    // and exit 2 instead of an unhandled exception (exit 134) whose stack names the path.
    [Fact]
    public async Task A_ui_path_that_is_a_file_ends_the_launch_with_SND_UI_004()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "ui"), "not a directory", Token);
        using var errors = new StringWriter();
        var log = new RecordingLogger();
        await using var instance = Launch();

        var exit = SingleLaunch.Run(instance, [], () => 0, errors, log);

        Assert.Equal(2, exit);
        Assert.DoesNotContain(_dir, errors.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(_dir, Assert.Single(log.Entries).Message, StringComparison.Ordinal);
    }
}
