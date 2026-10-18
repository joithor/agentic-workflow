namespace Aw.Sindri.Infrastructure.Sandbox;

/// <summary>One launch's running listeners; disposing stops them and removes the socket directory.</summary>
internal sealed class RunningEgress(string socat, string directory, EgressLog log, EgressListener proxy, EgressListener sindri) : IAsyncDisposable
{
    /// <summary>The socat to put in the init script.</summary>
    public string Socat { get; } = socat;

    /// <summary>The egress log.</summary>
    public EgressLog Log { get; } = log;

    /// <summary>The short socket directory.</summary>
    public string Directory { get; } = directory;

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        await proxy.DisposeAsync().ConfigureAwait(false);
        await sindri.DisposeAsync().ConfigureAwait(false);
        Log.Flush();
        System.IO.Directory.Delete(Directory, recursive: true);
    }
}
