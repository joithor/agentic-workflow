using Aw.Sindri.Ui.Platform;

namespace Aw.Sindri.Ui.Desktop;

/// <summary>The desktop adapter of <see cref="ISingleInstance"/>: a lock file's lock is the claim, and a Unix domain socket beside it the channel.</summary>
/// <param name="paths">Where the lock and the socket live.</param>
/// <param name="clock">The clock for the channel's timeouts.</param>
internal sealed class SocketSingleInstance(UiStatePaths paths, TimeProvider clock) : ISingleInstance
{
    public ValueTask<bool> TryClaim(CancellationToken cancellationToken) => throw new NotSupportedException($"{paths}{clock}");

    public ValueTask<bool> Send(string message, CancellationToken cancellationToken) => throw new NotSupportedException();

    public IAsyncEnumerable<string> Messages(CancellationToken cancellationToken) => throw new NotSupportedException();

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
