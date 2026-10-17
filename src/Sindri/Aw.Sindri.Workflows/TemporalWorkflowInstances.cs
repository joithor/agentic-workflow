using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Engine.Desk;
using Aw.Sindri.Core.Workflow.Recipes;
using Temporalio.Client;
using Temporalio.Common;
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

    // why: the query is built from quoted literals only (InstanceFilters.ToQuery), so it never fails as a query
    public ValueTask<Result<IReadOnlyList<InstanceRow>>> List(InstanceFilter filter, bool ended, CancellationToken cancellationToken) =>
        List(InstanceFilters.ToQuery(filter), ended, cancellationToken);

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
            foreach (var execution in await TemporalReads.List(client.Value, query is { Length: > 0 } ? $"({query}) AND {(ended ? Recipes : Open)}" : ended ? Recipes : Open, cancellationToken).ConfigureAwait(false))
            {
                rows.Add(Row(execution));
            }
        }
        catch (RpcException e) when (e.Code == RpcException.StatusCode.InvalidArgument)
        {
            return SindriErrors.InvalidInput.With($"'{query}' isn't a visibility query: {e.Message} (fix: e.g. --query \"NeedsYou = true AND State = 'checks'\")");
        }
        catch (RpcException e)
        {
            return SindriErrors.NotRunning.With($"Temporal didn't answer the list: {e.Message} (fix: sindri doctor)");
        }

        return rows.OrderBy(static r => r.Address, StringComparer.Ordinal).ToList();
    }

    private static InstanceRow Row(WorkflowExecution execution) => Row(execution.Id, execution.StartTime, execution.TypedSearchAttributes);

    /// <summary>
    /// The row of an execution from its search attributes. An attribute the instance never set reads as its default: an
    /// instance started before 'Urgency', 'Subject' and 'Root' existed is 'work', has no subject and is a root, which is
    /// also what the typed filters match it as (<see cref="InstanceFilters.ToQuery"/>: 'Urgency IS NULL', 'Root IS NULL').
    /// </summary>
    internal static InstanceRow Row(string id, DateTime startTime, SearchAttributeCollection attributes) => new(
        id,
        attributes.TryGetValue(SindriTemporal.StateKey, out var state) ? state : null,
        attributes.TryGetValue(SindriTemporal.NeedsYouKey, out var needsYou) && needsYou,
        new DateTimeOffset(DateTime.SpecifyKind(startTime, DateTimeKind.Utc)),
        attributes.TryGetValue(SindriTemporal.RecipeKey, out var recipe) ? recipe : null,
        attributes.TryGetValue(SindriTemporal.UrgencyKey, out var urgency) && Urgencies.IsKnown(urgency) ? Urgencies.Parse(urgency) : RecipeUrgency.Work,
        attributes.TryGetValue(SindriTemporal.SubjectKey, out var subject) ? subject : null,
        !attributes.TryGetValue(SindriTemporal.RootKey, out var root) || root);
}
