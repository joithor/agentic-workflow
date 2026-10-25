using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>Runs one Step input and shapes what it returned (plan decisions 2 and 7): the one code path the activity and the pull route's refill share.</summary>
public interface IStepInputRunner
{
    /// <summary>Runs the input in its own scope, scrubs, caps and hashes what it filled.</summary>
    /// <param name="context">What the input fills from.</param>
    /// <param name="index">The input's 1-based place in the Step's list.</param>
    /// <param name="cancellationToken">Cancels the fill.</param>
    /// <returns>The fill; SND-WF-016 when the build has no runtime for the input, SND-WF-019 when the plan is invalid.</returns>
    public ValueTask<Result<FilledOutput>> Fill(StepInputContext context, int index, CancellationToken cancellationToken);
}
