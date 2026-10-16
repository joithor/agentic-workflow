using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine.Desk;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>
/// The 'GetInstance' view of the instances a list shows, remembering the ones that ended: an ended instance's view never changes, and
/// reading it replays the whole workflow, so a list that includes every ended instance reads each of them once, not on every call.
/// A view is remembered by its address and start time (a new run on the same address starts later and is read afresh), only once it has an outcome.
/// </summary>
/// <param name="reader">The 'GetInstance' Query.</param>
public sealed class EndedInstanceViews(IInstanceReader reader)
{
    /// <summary>The most ended views kept; past it the memory starts over.</summary>
    public const int Capacity = 512;

    /// <summary>The view of <paramref name="address"/>, always read: this is the lookup of one instance, not a list row.</summary>
    /// <param name="address">The instance.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The view, or the reader's error.</returns>
    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) => reader.Get(address, cancellationToken);

    /// <summary>The view of a listed row: remembered when the instance had ended, else read.</summary>
    /// <param name="row">The listed row (its start time keys the memory).</param>
    /// <param name="address">The row's instance address.</param>
    /// <param name="cancellationToken">Cancels the query.</param>
    /// <returns>The view, or the reader's error.</returns>
    public ValueTask<Result<InstanceView>> Get(InstanceRow row, InstanceAddress address, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(row);
        return reader.Get(address, cancellationToken);
    }
}
