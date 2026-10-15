using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// The task queue a Step's activity runs on (README X61, the seam for a per-Step queue): today every Step runs on the
/// instance's own queue, the settings' task queue it was started on. A later phase that routes a Step to another
/// worker (a machine with the repo, a remote runner) decides it here, from the state alone, so replay stays deterministic.
/// </summary>
internal static class StepTaskQueue
{
    /// <summary>The queue for the Step <paramref name="state"/> runs.</summary>
    /// <param name="state">The active state.</param>
    /// <param name="instanceQueue">The queue the instance's workflow runs on.</param>
    /// <returns>The activity's task queue.</returns>
    public static string For(RecipeState state, string instanceQueue)
    {
        ArgumentNullException.ThrowIfNull(state);
        return instanceQueue;
    }
}
