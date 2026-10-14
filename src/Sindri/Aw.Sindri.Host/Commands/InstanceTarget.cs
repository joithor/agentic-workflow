using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Features.IssueNonce;
using Aw.Sindri.Features.ListInstances;

namespace Aw.Sindri.Host.Commands;

/// <summary>Where a CLI write goes (engine spec §6.3): the instance's opaque id for an address or bare item, and the command's nonce.</summary>
internal static class InstanceTarget
{
    /// <summary>
    /// The instance a command on <paramref name="reference"/> goes to, and its nonce (spec §5.2, §6.3): the address or
    /// bare item resolves to the instance's opaque id through 'GET /workflow/instances?address=', the only place an address
    /// travels; the nonce is the given one, else one the instance's nonce route issues. A human command starts the control
    /// plane first when this CLI may (API spec §7).
    /// </summary>
    internal static async Task<Result<(WorkflowInstanceId Id, CommandNonce Nonce)>> Resolve(RecipeDesk desk, string reference, CommandNonce? given, CancellationToken ct)
    {
        if (await desk.EnsureRunning(ct).ConfigureAwait(false) is { } down)
        {
            return down;
        }

        var listed = await desk.Client.Get<ListInstancesResponse>($"/sindri/workflow/instances?address={Uri.EscapeDataString(reference)}", ct).ConfigureAwait(false);
        if (listed.IsError)
        {
            return listed.Error;
        }

        if (listed.Value.Value.Instances is not [var instance])
        {
            return WorkflowErrors.InstanceNotFound.With($"'{reference}' names no one instance (fix: sindri workflow list)");
        }

        if (given is { } nonce)
        {
            return (instance.Id, nonce);
        }

        var issued = await desk.Client.Post<IssueNonceRequest, IssueNonceResponse>($"/sindri/workflow/instances/{instance.Id.Value}/nonce", body: null, ct)
            .ConfigureAwait(false);
        return issued.IsError ? issued.Error : (instance.Id, issued.Value.Value.Nonce);
    }
}
