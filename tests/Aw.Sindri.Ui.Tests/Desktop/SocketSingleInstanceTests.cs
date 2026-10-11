using System.Net.Sockets;
using Aw.Kernel.Testing;
using Aw.Sindri.Ui.Desktop;
using Aw.Sindri.Ui.Platform;
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
        _ = Directory.CreateDirectory(Paths.Directory);
        using (var crashed = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified))
        {
            crashed.Bind(new UnixDomainSocketEndPoint(Paths.InstanceSocket));
        }

        Assert.True(File.Exists(Paths.InstanceSocket));
        await using var first = Launch();
        await using var second = Launch();

        Assert.True(await first.TryClaim(Token));
        Assert.True(await second.Send(InstanceMessages.Focus, Token));
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
    }

    [Fact]
    public async Task Ending_the_instance_removes_its_socket()
    {
        var first = Launch();
        Assert.True(await first.TryClaim(Token));

        await first.DisposeAsync();

        Assert.False(File.Exists(Paths.InstanceSocket));
    }

    // A client that skips the adapter's own length check: the listener refuses what it reads and keeps serving.
    [Theory]
    [InlineData(InstanceMessages.MaxLength + 1, (byte)'x')]
    [InlineData(4, (byte)0xff)]
    [InlineData(0, (byte)'x')]
    public async Task The_listener_refuses_a_message_over_the_limit_or_not_utf8_or_empty(int length, byte fill)
    {
        await using var first = Launch();
        await using var second = Launch();
        Assert.True(await first.TryClaim(Token));

        Assert.Equal(0, await RawSend([.. Enumerable.Repeat(fill, length)]));
        Assert.True(await second.Send(InstanceMessages.Focus, Token));

        await using var messages = first.Messages(Token).GetAsyncEnumerator(Token);
        Assert.True(await messages.MoveNextAsync());
        Assert.Equal(InstanceMessages.Focus, messages.Current);
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
}
