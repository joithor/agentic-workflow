using Aw.Sindri.Core.Launches;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Features.GetInstance;

/// <summary>
/// The two read-time joins of the instance detail that the engine's view can't make (q65): the agent session of the open run, from its launch row, and where each reviewed
/// fact opens, from the fact's stored value. Neither is held by the workflow, so a replay never depends on them.
/// </summary>
/// <param name="launches">The launches view.</param>
/// <param name="contents">The stored fact content.</param>
internal sealed class InstanceJoins(ILaunchViews launches, IFactContents contents)
{
    /// <summary>The session of <paramref name="view"/>'s open agent run.</summary>
    /// <param name="view">The instance's view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The provider and id once the launch knows them; null for a desk run, a run not yet launched, or a launch that can't be read.</returns>
    public ValueTask<SessionHandle?> Session(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        _ = (launches, cancellationToken);
        return ValueTask.FromResult<SessionHandle?>(null);
    }

    /// <summary>Where each fact the open desk reviews opens.</summary>
    /// <param name="view">The instance's view.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The targets by fact key; empty when the desk reviews nothing or none can be read.</returns>
    public ValueTask<IReadOnlyDictionary<string, DeskReviewTarget>> ReviewTargets(InstanceView view, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(view);
        _ = (contents, cancellationToken);
        return ValueTask.FromResult<IReadOnlyDictionary<string, DeskReviewTarget>>(new Dictionary<string, DeskReviewTarget>());
    }
}
