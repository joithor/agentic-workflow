using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Workflow;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Temporalio.Api.Enums.V1;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IInstanceDirectory"/> over the Temporal client (engine spec rev 8 §5.1, §6.1; engine P4 unit (b)). The
/// attempts of a '(recipe, item)' pair are read by describing their workflow ids, '{item}/{recipe}/1', '/2' … up to the
/// first that doesn't exist, so a start never waits on eventually consistent visibility. A bare item is resolved through
/// the 'Item' search attribute.
/// </summary>
/// <param name="frontend">Finds the dev server serve keeps.</param>
internal sealed class TemporalInstanceDirectory(TemporalFrontend frontend) : IInstanceDirectory
{
    public async ValueTask<Result<AttemptState?>> Latest(ItemKey item, RecipeId recipe, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        AttemptState? latest = null;
        for (var attempt = 1; ; attempt++)
        {
            var address = InstanceAddress.Root(item, recipe, attempt);
            try
            {
                var described = await client.Value.GetWorkflowHandle(address.Value).DescribeAsync(new() { Rpc = new() { CancellationToken = cancellationToken } }).ConfigureAwait(false);
                latest = new AttemptState(address, attempt, described.Status == WorkflowExecutionStatus.Running);
            }
            catch (RpcException e) when (e.Code == RpcException.StatusCode.NotFound) // boundary: the first attempt that was never started ends the scan
            {
                return latest;
            }
        }
    }

    public async ValueTask<Result<InstanceAddress>> Resolve(string reference, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(reference);
        if (InstanceAddress.TryFrom(reference, out var address))
        {
            return address;
        }

        if (!ItemKey.TryFrom(reference, out var item))
        {
            return WorkflowErrors.InstanceNotFound.With($"'{reference}' is neither an address ('T-3/step/1') nor an item (fix: sindri workflow list)");
        }

        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var roots = new Dictionary<string, (int Attempt, InstanceAddress Address)>(StringComparer.Ordinal);
        await foreach (var execution in client.Value.ListWorkflowsAsync($"WorkflowType = 'RecipeWorkflow' AND Item = '{item.Value}'").WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            if (execution.Id.Split('/') is [_, var recipe, var number] && int.TryParse(number, NumberStyles.None, CultureInfo.InvariantCulture, out var attempt)
                && (!roots.TryGetValue(recipe, out var seen) || seen.Attempt < attempt))
            {
                roots[recipe] = (attempt, InstanceAddress.From(execution.Id));
            }
        }

        return roots.Count switch
        {
            0 => WorkflowErrors.InstanceNotFound.With($"{item.Value} has no instance (fix: sindri workflow start <recipe> --item {item.Value})"),
            1 => roots.Values.Single().Address,
            _ => WorkflowErrors.AmbiguousAddress.With(
                $"{item.Value} has instances of more than one recipe: {string.Join(", ", roots.Values.Select(static r => r.Address.Value).Order(StringComparer.Ordinal))} (fix: use one of these addresses)"),
        };
    }
}
