using Aw.Sindri.Ui.Api;

namespace Aw.Sindri.Ui.ViewModels.Workflows;

/// <summary>The list's answer to an instance change (<see cref="WorkflowsViewModel"/>, continued).</summary>
internal sealed partial class WorkflowsViewModel
{
    /// <summary>Re-reads the list for a change of an instance (or for any change when <paramref name="change"/> is null).</summary>
    /// <param name="url">The API's base URL.</param>
    /// <param name="change">The frame, or null to re-read.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>False when the row of that instance still isn't in the frame's state; true when it is, or when no row shows it.</returns>
    internal Task<bool> Catch(Uri url, InstanceChange? change, CancellationToken cancellationToken) => throw new NotSupportedException();
}
