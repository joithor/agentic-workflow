namespace Aw.Context.Testing.Fakes;

/// <summary>A fake Onyx server on loopback that replays the split-panes recordings. See the fixture README.</summary>
public sealed class FakeOnyxServer : IAsyncDisposable
{
    private FakeOnyxServer()
    {
    }

    private string Red => GetHashCode().ToString(System.Globalization.CultureInfo.InvariantCulture);

    /// <summary>The base URL, 'http://127.0.0.1:&lt;port&gt;/'.</summary>
    public Uri Url => throw new NotSupportedException($"red: url {Red}");

    /// <summary>The credential the server accepts.</summary>
    public string Token => throw new NotSupportedException($"red: token {Red}");

    /// <summary>The 'Authorization' header value the server accepts: 'Bearer &lt;token&gt;'.</summary>
    public string Authorization => throw new NotSupportedException($"red: authorization {Red}");

    /// <summary>The requests received, in order (no headers).</summary>
    public IReadOnlyList<RecordedRequest> Requests => throw new NotSupportedException($"red: requests {Red}");

    /// <summary>Every log line the server and its web host wrote (kept in memory, never printed).</summary>
    public IReadOnlyList<string> Log => throw new NotSupportedException($"red: log {Red}");

    /// <summary>Starts the server on an ephemeral loopback port.</summary>
    /// <param name="options">Options; null for the defaults.</param>
    /// <returns>The running server.</returns>
    public static Task<FakeOnyxServer> Start(FakeServerOptions? options = null) =>
        throw new NotSupportedException($"red: start with {options}");

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
