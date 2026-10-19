using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine.Effects;

namespace Aw.Sindri.Infrastructure.Workflow;

/// <summary>
/// The claim's marker ref through git (engine spec §12 'effect.claim.ref'; engine P6 unit (d)): 'git update-ref' creates
/// the ref, with an empty old value so it never moves one that exists, at the default branch's head: the remote's
/// default ('origin/HEAD') when the repo has one, else 'main', else 'master', else the checkout's HEAD. No branch, index
/// or working-tree file is touched. Each git call is bounded to 30 s.
/// </summary>
internal sealed class GitClaimRefs : IClaimRefs
{
    private static readonly string[] _defaultBranches = ["refs/remotes/origin/HEAD", "refs/heads/main", "refs/heads/master", "HEAD"];

    public async ValueTask<Result<bool>> Mark(string repository, string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(reference);
        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is { Exit: 0 })
        {
            return false;   // the marker exists (an earlier claim, or this one retried): reused as it is
        }

        string? head = null;
        foreach (var branch in _defaultBranches)
        {
            if (await Commit(repository, branch, cancellationToken).ConfigureAwait(false) is { Exit: 0, Output: var sha })
            {
                head = sha;
                break;
            }
        }

        if (head is null)
        {
            return WorkflowErrors.NoRuntime.With($"{repository} has no commit to point {reference} at (fix: commit once on its default branch, then resume the parked instance to send the claim again)");
        }

        var created = await GitProcess.Run(repository, ["update-ref", "-m", "sindri: claim", reference, head, ""], cancellationToken).ConfigureAwait(false);
        if (created.Exit == 0)
        {
            return true;
        }

        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is { Exit: 0 })
        {
            return false;   // created by a concurrent take between the check and the write
        }

        // A lock another git holds passes: the error is retryable (the effect retries it). Anything else can't be fixed by trying again.
        return created.Error.Contains(".lock", StringComparison.Ordinal)
            ? WorkflowErrors.EffectFailed.With($"git update-ref {reference} in {repository} is locked: {created.Error.Trim()} (retrying)")
            : WorkflowErrors.NoRuntime.With($"git update-ref {reference} failed in {repository}: {created.Error.Trim()} (fix: check the repo, then resume the parked instance to send the claim again)");
    }

    public async ValueTask<Result<bool>> Unmark(string repository, string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(reference);
        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is not { Exit: 0 })
        {
            return false;   // no marker (never made, or an earlier cleanup took it): nothing to do
        }

        var deleted = await GitProcess.Run(repository, ["update-ref", "-m", "sindri: release claim", "-d", reference], cancellationToken).ConfigureAwait(false);
        if (deleted.Exit == 0)
        {
            return true;
        }

        if (await Commit(repository, reference, cancellationToken).ConfigureAwait(false) is not { Exit: 0 })
        {
            return false;   // deleted by a concurrent cleanup between the check and the write
        }

        return deleted.Error.Contains(".lock", StringComparison.Ordinal)
            ? WorkflowErrors.EffectFailed.With($"git update-ref -d {reference} in {repository} is locked: {deleted.Error.Trim()} (retrying)")
            : WorkflowErrors.NoRuntime.With($"git update-ref -d {reference} failed in {repository}: {deleted.Error.Trim()}");
    }

    /// <summary>The commit <paramref name="name"/> names, if it names one.</summary>
    private static Task<GitRun> Commit(string repository, string name, CancellationToken cancellationToken) =>
        GitProcess.Run(repository, ["rev-parse", "--verify", "--quiet", name + "^{commit}"], cancellationToken);
}
