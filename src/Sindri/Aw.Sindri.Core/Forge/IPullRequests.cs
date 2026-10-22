using Aw.Kernel;

namespace Aw.Sindri.Core.Forge;

/// <summary>
/// The pull requests Sindri follows and opens (PR babysitter spec PB10 and §10.2, BD1): Sindri's own port over the forge, with only what its
/// steps and effects need and Sindri's types and strings. Its adapters sit in the Host, the one project that may reference the forge package.
/// Everything the forge returns (a check's log, a title) is untrusted text. Every member answers an error rather than throwing: SND-FORGE-001
/// when the approved profile names no forge, SND-FORGE-002 when the forge or the profile's host list refuses the call, SND-FORGE-003 when the
/// pull request isn't there, SND-FORGE-004 when the forge can't be reached.
/// </summary>
public interface IPullRequests
{
    /// <summary>Reads a pull request: its state, its head, its checks and what asks for attention.</summary>
    /// <param name="link">The pull request's link, as 'fact:pr' holds it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The pull request, or the error.</returns>
    public ValueTask<Result<PullView>> Get(string link, CancellationToken cancellationToken);

    /// <summary>Reads the tail of one check's log, capped and scrubbed by the forge port. The text is untrusted.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="check">The check's name, as <see cref="PullChecks.Failing"/> lists it.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The excerpt, or the error.</returns>
    public ValueTask<Result<string>> CheckLog(string link, string check, CancellationToken cancellationToken);

    /// <summary>Pushes a workspace commit to the pull request's head branch, with a lease on the tip the caller saw. Never rebases, never forces past the lease, never pushes to a fork.</summary>
    /// <param name="push">The push.</param>
    /// <param name="cancellationToken">Cancels the push.</param>
    /// <returns>How it ended when git itself worked, or the error.</returns>
    public ValueTask<Result<PullPushOutcome>> Push(PullPush push, CancellationToken cancellationToken);

    /// <summary>Asks the forge to run the failed checks of a commit again.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="sha">The commit whose failed checks run again.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>How many checks were asked to run again, or the error.</returns>
    public ValueTask<Result<int>> RerunChecks(string link, string sha, CancellationToken cancellationToken);

    /// <summary>Lists the pull request's review threads, review bodies and top-level comments, oldest first. The texts are untrusted.</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The threads, or the error.</returns>
    public ValueTask<Result<IReadOnlyList<PullThread>>> Threads(string link, CancellationToken cancellationToken);

    /// <summary>Finds the open pull request for a head branch (and the marker its body carries, when the query names one).</summary>
    /// <param name="query">What to look for.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The pull request, a success with null when there is none, or the error.</returns>
    public ValueTask<Result<OpenedPull?>> Find(FindPull query, CancellationToken cancellationToken);

    /// <summary>Opens a pull request. The head branch must already be on the remote at the head sha. A marker is added to the body, so Find can recognise the pull request later.</summary>
    /// <param name="request">The pull request to open.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>The new pull request, or the error.</returns>
    public ValueTask<Result<OpenedPull>> Open(OpenPull request, CancellationToken cancellationToken);

    /// <summary>Changes a pull request's title and body; a null keeps what it has. A new body replaces the old one whole (the marker too, so the caller keeps it).</summary>
    /// <param name="link">The pull request's link.</param>
    /// <param name="title">The new title, or null.</param>
    /// <param name="body">The new body, or null.</param>
    /// <param name="cancellationToken">Cancels the call.</param>
    /// <returns>Success, or the error.</returns>
    public ValueTask<Result<Unit>> Update(string link, string? title, string? body, CancellationToken cancellationToken);
}
