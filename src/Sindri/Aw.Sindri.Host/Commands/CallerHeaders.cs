using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>The caller headers the CLI sends on every API call (API spec §6, "Who sends what").</summary>
internal sealed class CallerHeaders
{
    private readonly AgentSession? _session;
    private readonly IHumanTokenStore? _tokens;
    private readonly Func<CancellationToken, ValueTask<RepoName?>> _repo;
    private (bool Found, RepoName? Repo) _located;

    private CallerHeaders(AgentSession? session, IHumanTokenStore? tokens, Func<CancellationToken, ValueTask<RepoName?>> repo)
    {
        _session = session;
        _tokens = tokens;
        _repo = repo;
    }

    /// <summary>No headers at all (what S1's client sent): the API reads it as a session.</summary>
    internal static CallerHeaders None { get; } = new(session: null, tokens: null, static _ => ValueTask.FromResult<RepoName?>(null));

    /// <summary>Why this CLI calls as a session, or null for a human (or for <see cref="None"/>).</summary>
    internal string? SessionReason => _session?.SessionReason;

    /// <summary>The headers for <paramref name="session"/>.</summary>
    /// <param name="session">How the CLI classed itself.</param>
    /// <param name="tokens">Where the human token is read from, at each call (only for a human); none: no token is sent.</param>
    /// <param name="repo">The X-Sindri-Repo value, found once at the first call.</param>
    /// <returns>The headers.</returns>
    internal static CallerHeaders For(AgentSession session, IHumanTokenStore? tokens, Func<CancellationToken, ValueTask<RepoName?>> repo) =>
        new(session, tokens, repo);

    /// <summary>Adds the headers to <paramref name="request"/>.</summary>
    /// <param name="request">The API call.</param>
    /// <param name="cancellationToken">Cancels the token read and the repo lookup.</param>
    /// <returns>The work.</returns>
    internal async ValueTask Apply(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (_session is not { } session)
        {
            return;
        }

        var headers = request.Headers;
        headers.Add(SindriHeaders.Caller, session.Human ? "human" : "session");
        headers.Add(SindriHeaders.Client, session.Human && session.Prover ? "prover" : "cli");
        if (session.Human)
        {
            headers.Add(SindriHeaders.Terminal, "1");
            if (_tokens is not null && await _tokens.Read(cancellationToken).ConfigureAwait(false) is { IsSuccess: true, Value: { } token })
            {
                headers.Add(SindriHeaders.Token, token.Value);   // a missing or unreadable token is sent as none: the API answers SND-AUTH-001
            }
        }

        if (await Repo(cancellationToken).ConfigureAwait(false) is { } repo)
        {
            headers.Add(SindriHeaders.Repo, repo.Value);
        }
    }

    /// <summary>The repo <paramref name="directory"/> belongs to among the approved profile's repos (API spec §3.1).</summary>
    /// <param name="approvals">The approved profile.</param>
    /// <param name="locator">Resolves the main checkout and matches it.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The repo, or null (no approved profile, no repos, outside every repo).</returns>
    internal static async ValueTask<RepoName?> RepoOf(IApprovals approvals, IRepoLocator locator, string directory, CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        return approved is { IsSuccess: true, Value.Profile.Repos: { Count: > 0 } repos }
            ? await locator.Locate(directory, repos, cancellationToken).ConfigureAwait(false)
            : null;
    }

    private async ValueTask<RepoName?> Repo(CancellationToken cancellationToken)
    {
        if (!_located.Found)
        {
            _located = (true, await _repo(cancellationToken).ConfigureAwait(false));
        }

        return _located.Repo;
    }
}
