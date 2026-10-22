using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The phase worktree 'step.phase.seed' made (line spec LN-WF2): which stored 'fact:workspace' an ended instance gives back, and the path a stored workspace names.</summary>
internal static class PhaseWorkspace
{
    /// <summary>The workspace an ended instance removes: only a 'workflow.phase' instance owns one, and it keeps it while its pull request may still be open.</summary>
    /// <param name="recipe">The instance's recipe.</param>
    /// <param name="facts">The instance's facts.</param>
    /// <param name="outcome">The terminal outcome; null when the instance did not reach one (a cancel).</param>
    /// <returns>The stored workspace to remove; null when the instance owns none or keeps it.</returns>
    public static FactValue? ToRemove(RecipeId recipe, IReadOnlyDictionary<FactKey, FactValue> facts, OutcomeName? outcome) =>
        throw new NotSupportedException();

    /// <summary>The worktree path a stored 'fact:workspace' holds (plain text or a JSON string).</summary>
    /// <param name="contents">Reads the stored fact.</param>
    /// <param name="stored">The stored fact.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The path, or the read's error.</returns>
    public static async ValueTask<Result<string>> PathOf(IFactContents contents, FactValue stored, CancellationToken cancellationToken)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        throw new NotSupportedException();
    }
}
