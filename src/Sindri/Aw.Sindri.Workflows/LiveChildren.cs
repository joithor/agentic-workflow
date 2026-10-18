using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>Reads each child of a parent's status live (engine P7 plan, owner answer Q2).</summary>
internal static class LiveChildren
{
    /// <summary>What a child that couldn't be reached shows as its state.</summary>
    public const string Unreachable = "unreachable";

    /// <summary>The parent's view with every child's state as that child answers now.</summary>
    /// <param name="view">The parent's own view.</param>
    /// <param name="query">Asks one child for its view.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The view; the same one when it lists no child.</returns>
    public static Task<InstanceView> Of(InstanceView view, Func<InstanceAddress, CancellationToken, Task<InstanceView>> query, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P7 U2");
}
