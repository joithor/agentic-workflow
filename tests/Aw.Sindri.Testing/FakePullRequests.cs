using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing;

/// <summary>
/// The in-memory <see cref="IPullRequests"/> for what opens pull requests: Find, Open and Update over a list of open pulls, each remembering the request that made it.
/// Get, CheckLog, Push and RerunChecks are not part of what it fakes.
/// </summary>
public sealed class FakePullRequests : IPullRequests
{
    private readonly List<(OpenPull Request, OpenedPull Pull)> _open = [];

    /// <summary>When set, Find, Open and Update answer this error.</summary>
    public Error? Failure { get; set; }

    /// <summary>The requests Open received, in order.</summary>
    public List<OpenPull> Opened { get; } = [];

    /// <summary>The (link, title, body) triples Update received, in order.</summary>
    public List<(string Link, string? Title, string? Body)> Updated { get; } = [];

    /// <summary>Puts an open pull request on the fake forge.</summary>
    /// <param name="request">What it was opened with.</param>
    /// <returns>The pull request.</returns>
    public OpenedPull Seed(OpenPull request)
    {
        var pull = new OpenedPull($"https://{request.Repo.Host}/{request.Repo.Owner}/{request.Repo.Name}/pull/{(_open.Count + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)}", _open.Count + 1);
        _open.Add((request, pull));
        return pull;
    }

    /// <inheritdoc/>
    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<OpenedPull?>>(failure);
        }

        var found = _open.FirstOrDefault(o => o.Request.Repo == query.Repo && string.Equals(o.Request.Head, query.Head, StringComparison.Ordinal)
            && (query.Marker is null || string.Equals(o.Request.Marker, query.Marker, StringComparison.Ordinal)));
        return ValueTask.FromResult<Result<OpenedPull?>>(found.Pull);
    }

    /// <inheritdoc/>
    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<OpenedPull>>(failure);
        }

        Opened.Add(request);
        return ValueTask.FromResult<Result<OpenedPull>>(Seed(request));
    }

    /// <inheritdoc/>
    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
    {
        if (Failure is { } failure)
        {
            return ValueTask.FromResult<Result<Unit>>(failure);
        }

        Updated.Add((link, title, body));
        return ValueTask.FromResult<Result<Unit>>(Results.Ok());
    }

    /// <inheritdoc/>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken) => throw new NotSupportedException();

    /// <inheritdoc/>
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken) => throw new NotSupportedException();
}
