using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Host.Commands;

/// <summary>The caller headers the CLI sends on every API call (API spec §6, "Who sends what").</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "MA0182:Unused internal type", Justification = "red commit: ControlPlaneClient takes it in the green one")]
internal sealed class CallerHeaders
{
    private readonly AgentSession? _session;
    private readonly IHumanTokenStore? _tokens;
    private readonly Func<CancellationToken, ValueTask<RepoName?>> _repo;

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
    /// <param name="tokens">Where the human token is read from, at each call (only for a human).</param>
    /// <param name="repo">The X-Sindri-Repo value, found once at the first call.</param>
    /// <returns>The headers.</returns>
    internal static CallerHeaders For(AgentSession session, IHumanTokenStore tokens, Func<CancellationToken, ValueTask<RepoName?>> repo) =>
        new(session, tokens, repo);

    /// <summary>Adds the headers to <paramref name="request"/>.</summary>
    /// <param name="request">The API call.</param>
    /// <param name="cancellationToken">Cancels the token read and the repo lookup.</param>
    /// <returns>The work.</returns>
    internal ValueTask Apply(HttpRequestMessage request, CancellationToken cancellationToken) =>
        throw new NotSupportedException($"red: caller, terminal, client, token and repo headers for {_session}, {_tokens is null}, {_repo.Method.Name}");

    /// <summary>The repo <paramref name="directory"/> belongs to among the approved profile's repos (API spec §3.1).</summary>
    /// <param name="approvals">The approved profile.</param>
    /// <param name="locator">Resolves the main checkout and matches it.</param>
    /// <param name="directory">The working directory.</param>
    /// <param name="cancellationToken">Cancels the work.</param>
    /// <returns>The repo, or null (no approved profile, no repos, outside every repo).</returns>
    internal static ValueTask<RepoName?> RepoOf(IApprovals approvals, IRepoLocator locator, string directory, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: the approved repos, then the locator");
}
