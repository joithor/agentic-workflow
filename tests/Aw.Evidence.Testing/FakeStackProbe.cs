using System.Globalization;
using Aw.Evidence.Core;
using Aw.Evidence.Core.Stack;
using Aw.Kernel;

namespace Aw.Evidence.Testing;

/// <summary>A scripted IStackProbe: a title (null fails), a database switch and the open ports.</summary>
public sealed class FakeStackProbe : IStackProbe
{
    /// <summary>The page title; null makes the fetch fail.</summary>
    public string? Title { get; set; } = "App";

    /// <summary>Whether the database accepts connections.</summary>
    public bool DatabaseReady { get; set; } = true;

    /// <summary>Open host:port pairs.</summary>
    public ISet<string> OpenPorts { get; } = new HashSet<string>(StringComparer.Ordinal) { "localhost:5002" };

    /// <summary>The slot databases that exist.</summary>
    public ISet<string> Databases { get; } = new HashSet<string>(StringComparer.Ordinal);

    /// <summary>The directory each listening port is served from (a port that is absent has no listener).</summary>
    public IDictionary<int, string> ListenerDirectories { get; } = new Dictionary<int, string>();

    /// <inheritdoc/>
    public ValueTask<Result<string>> PageTitleAsync(Uri url, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string>>(Title is null ? EvidenceErrors.ProbeFailed.With($"{url} did not answer") : Title);

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DatabaseReadyAsync(CancellationToken cancellationToken) =>
        ValueTask.FromResult(DatabaseReady ? Results.Ok() : EvidenceErrors.ProbeFailed.With("database is not ready"));

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> PortOpenAsync(string host, int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult(OpenPorts.Contains(string.Create(CultureInfo.InvariantCulture, $"{host}:{port}")) ? Results.Ok() : EvidenceErrors.ProbeFailed.With(string.Create(CultureInfo.InvariantCulture, $"{host}:{port} refused the connection")));

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> DatabaseExistsAsync(string database, CancellationToken cancellationToken) =>
        ValueTask.FromResult(Databases.Contains(database) ? Results.Ok() : EvidenceErrors.ProbeFailed.With($"database {database} does not exist"));

    /// <inheritdoc/>
    public ValueTask<Result<string?>> ListenerDirectoryAsync(int port, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<string?>>(ListenerDirectories.TryGetValue(port, out var directory) ? directory : null);
}
