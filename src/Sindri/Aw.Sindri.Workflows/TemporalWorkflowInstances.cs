using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Temporalio.Exceptions;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>
/// <see cref="IWorkflowInstances"/> as a Temporal visibility query on the search attributes the interpreter upserts
/// (engine spec rev 8 §5.1). Engine P4a: the CLI asks the dev server control.json records; the caller's query is ANDed
/// with "the RecipeWorkflow instances" (and "running" unless ended ones are asked for, unit (e)).
/// </summary>
/// <param name="frontend">Finds the dev server.</param>
internal sealed class TemporalWorkflowInstances(TemporalFrontend frontend) : IWorkflowInstances
{
    private const string Recipes = "WorkflowType = 'RecipeWorkflow'";

    private const string Open = Recipes + " AND ExecutionStatus = 'Running'";

    public async ValueTask<Result<IReadOnlyList<InstanceRow>>> List(string? query, bool ended, CancellationToken cancellationToken)
    {
        var client = await frontend.Connect(flag: null, cancellationToken).ConfigureAwait(false);
        if (client.IsError)
        {
            return client.Error;
        }

        var rows = new List<InstanceRow>();
        try
        {
            await foreach (var execution in client.Value.ListWorkflowsAsync(query is { Length: > 0 } ? $"({query}) AND {(ended ? Recipes : Open)}" : ended ? Recipes : Open)
                .WithCancellation(cancellationToken).ConfigureAwait(false))
            {
                var attributes = execution.TypedSearchAttributes;
                rows.Add(new InstanceRow(
                    execution.Id,
                    attributes.TryGetValue(SindriTemporal.StateKey, out var state) ? state : null,
                    attributes.TryGetValue(SindriTemporal.NeedsYouKey, out var needsYou) && needsYou));
            }
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.InvalidArgument)
        {
            return SindriErrors.InvalidInput.With($"'{query}' isn't a visibility query: {e.Message} (fix: e.g. --query \"NeedsYou = true AND State = 'checks'\")");
        }

        return rows.OrderBy(static r => r.Address, StringComparer.Ordinal).ToList();
    }
}
