using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Forge;

namespace Aw.Sindri.Testing.Forge;

/// <summary>
/// An in-memory <see cref="IPullRequests"/> over a list of pull requests on allowed hosts. Held to <see cref="PullRequestsContract"/>. A test seeds a pull request
/// with <see cref="Add"/> and reads what the port did from <see cref="Calls"/> and <see cref="Pulls"/>.
/// </summary>
/// <param name="hosts">The hosts the profile allows.</param>
public sealed class FakePullRequests(IEnumerable<string> hosts) : IPullRequests
{
    private readonly HashSet<string> _hosts = new(hosts, StringComparer.Ordinal);
    private readonly List<FakePull> _pulls = [];

    /// <summary>Gets the operations called, in order.</summary>
    public IList<string> Calls { get; } = [];

    /// <summary>Gets the pull requests, with what Open and Update gave them.</summary>
    public IReadOnlyList<FakePull> Pulls => _pulls;

    /// <summary>Adds an open pull request.</summary>
    /// <param name="repo">The repository.</param>
    /// <param name="head">The head branch.</param>
    /// <param name="headSha">The head's tip.</param>
    /// <returns>The pull request.</returns>
    public FakePull Add(PullRepo repo, string head, string headSha)
    {
        ArgumentNullException.ThrowIfNull(repo);
        var pull = new FakePull(repo, _pulls.Count + 1, head, headSha);
        _pulls.Add(pull);
        return pull;
    }

    /// <inheritdoc />
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Get");
        return Done(Locate(link).Map(p => new PullView(p.Link, p.State, p.HeadSha, p.Mergeable, p.ReviewDecision, p.Threads.Count(static t => t.Kind == PullCommentKind.Thread && !t.Resolved), new PullChecks(p.ChecksState, p.ChecksSha ?? p.HeadSha, [.. p.FailingChecks]))));
    }

    /// <inheritdoc />
    public ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken)
    {
        Calls.Add("Threads");
        return Done(Locate(link).Map(p => (IReadOnlyList<PullThread>)[.. p.Threads]));
    }

    /// <inheritdoc />
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken)
    {
        Calls.Add("CheckLog");
        return Done(Locate(link).Map(_ => $"log of {check}"));
    }

    /// <inheritdoc />
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(push);
        Calls.Add("Push");
        return Done(Locate(push.Link).Map(p =>
        {
            if (p.HeadSha != push.LeaseSha)
            {
                return PullPushOutcome.HeadMoved;
            }

            p.HeadSha = push.Sha;
            return PullPushOutcome.Pushed;
        }));
    }

    /// <inheritdoc />
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken)
    {
        Calls.Add("RerunChecks");
        return Done(Locate(link).Map(_ => 0));
    }

    /// <inheritdoc />
    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        Calls.Add("Find");
        if (Refuse(query.Repo.Host) is { } refused)
        {
            return Done<OpenedPull?>(refused);
        }

        var found = _pulls.FirstOrDefault(p => p.State == PullState.Open && p.Repo == query.Repo && p.Head == query.Head && (query.Marker is null || p.Marker == query.Marker));
        return Done(Results.Success(found is null ? null : new OpenedPull(found.Link, found.Number)));
    }

    /// <inheritdoc />
    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Calls.Add("Open");
        if (Refuse(request.Repo.Host) is { } refused)
        {
            return Done<OpenedPull>(refused);
        }

        var pull = Add(request.Repo, request.Head, request.HeadSha);
        pull.Title = request.Title;
        pull.Body = request.Body;
        pull.Draft = request.Draft;
        pull.Marker = request.Marker;
        return Done(Results.Success(new OpenedPull(pull.Link, pull.Number)));
    }

    /// <inheritdoc />
    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken)
    {
        Calls.Add("Update");
        return Done(Locate(link).Map(p =>
        {
            p.Title = title ?? p.Title;
            p.Body = body ?? p.Body;
            return Unit.Value;
        }));
    }

    private static ValueTask<Result<T>> Done<T>(Result<T> result) => ValueTask.FromResult(result);

    private Error? Refuse(string host) =>
        _hosts.Contains(host) ? null : SindriErrors.ForgeRefused.With($"host '{host}' is not in the forge hosts");

    private Result<FakePull> Locate(string link)
    {
        if (!Uri.TryCreate(link, UriKind.Absolute, out var uri))
        {
            return SindriErrors.ForgeRefused.With("link: not an absolute URL");
        }

        if (Refuse(uri.IdnHost) is { } refused)
        {
            return refused;
        }

        var parts = uri.AbsolutePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts is not [var owner, var name, "pull", var number] || !int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var n))
        {
            return SindriErrors.ForgeRefused.With("path: expected owner/repo/pull/number");
        }

        var repo = new PullRepo(uri.IdnHost, owner, name);
        return _pulls.FirstOrDefault(p => p.Repo == repo && p.Number == n) is { } pull
            ? pull
            : SindriErrors.ForgeNotFound.With(string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $"no pull request {owner}/{name}#{n}"));
    }
}
