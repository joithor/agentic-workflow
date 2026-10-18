using System.Globalization;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Extract;
using Aw.Context.Infrastructure.Extract;
using Aw.Kernel;

namespace Aw.Context.Tests.Infrastructure;

/// <summary>
/// Runs a cluster pass under a test deadline. A pass is a few small processes (git archive, the stub or real graphify), seconds even on a
/// loaded box; a hang in one of them fails here with a message naming it, not at the test host's 5 minutes (or after git's own 5-minute
/// archive timeout, which the source turns into a quiet 'unavailable').
/// </summary>
internal static class GraphifyRun
{
    /// <summary>The longest one pass may take in a test.</summary>
    public static readonly TimeSpan Deadline = TimeSpan.FromSeconds(30);

    /// <summary>Runs <paramref name="source"/> over the repo's head.</summary>
    /// <param name="source">The cluster source under test.</param>
    /// <param name="repo">The repo to cluster.</param>
    /// <param name="limits">The limits of the pass.</param>
    /// <param name="deadline">The deadline; <see cref="Deadline"/> when null.</param>
    /// <returns>What the source answered; a TimeoutException when the pass hung.</returns>
    public static async Task<Result<ClusterSet>> Clusters(GraphifyClusterSource source, TempGitRepo repo, ClusterLimits limits, TimeSpan? deadline = null)
    {
        var limit = deadline ?? Deadline;
        var test = TestContext.Current.CancellationToken;
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(test);
        bounded.CancelAfter(limit);
        try
        {
            return await source.Clusters(repo.Repo, repo.Head, limits, bounded.Token);
        }
        catch (OperationCanceledException e) when (!test.IsCancellationRequested)
        {
            throw new TimeoutException(string.Create(CultureInfo.InvariantCulture, $"GraphifyClusterSource.Clusters did not finish in {limit.TotalSeconds} s: a child process (git archive or the graphify stub) is hung, or the box is starved"), e);
        }
    }
}
