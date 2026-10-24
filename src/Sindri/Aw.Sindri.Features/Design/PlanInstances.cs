using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Design;

/// <summary>Opens a workflow.plan instance for a read the caller is allowed (line spec LN-API2): an unknown instance, or another repo's for a session caller, answers SND-ITEM-404 (one answer); an instance of another recipe SND-LINE-003.</summary>
/// <param name="directory">Resolves the instance id to its address.</param>
/// <param name="runtime">The instances on Temporal.</param>
/// <param name="repos">Where each item belongs, from the ledger.</param>
/// <param name="callers">The caller, whose repo scope decides what it may read.</param>
internal sealed class PlanInstances(IInstanceDirectory directory, IInstanceReader runtime, IItemRepos repos, ICallerContextAccessor callers)
{
    private const string Recipe = "workflow.plan";

    /// <summary>The instance's view, when the caller may read it and it runs the plan recipe.</summary>
    internal ValueTask<Result<InstanceView>> Open(WorkflowInstanceId instance, CancellationToken cancellationToken) => Open(instance, Recipe, cancellationToken);

    /// <summary>The instance's view, when the caller may read it and it runs <paramref name="recipe"/> (the line board's read, LN-API1, opens a workflow.line instance the same way).</summary>
    internal async ValueTask<Result<InstanceView>> Open(WorkflowInstanceId instance, string recipe, CancellationToken cancellationToken)
    {
        if (callers.Current is not { } caller)
        {
            return SindriErrors.AuthRefused.With("a line read needs a caller");
        }

        var address = await directory.Resolve(instance.Value, cancellationToken).ConfigureAwait(false);
        if (address.IsError)
        {
            return InstanceScope.Unknown(address.Error, instance.Value);
        }

        var view = await runtime.Get(address.Value, cancellationToken).ConfigureAwait(false);
        if (view.IsError)
        {
            return InstanceScope.Unknown(view.Error, instance.Value);
        }

        if (await InstanceScope.Refusal(caller, view.Value.ItemId, instance.Value, repos, cancellationToken).ConfigureAwait(false) is { } refused)
        {
            return refused;
        }

        return string.Equals(view.Value.Recipe.Id.Value, recipe, StringComparison.Ordinal)
            ? view.Value
            : LineErrors.WrongRecipe.With($"{instance.Value} runs {view.Value.Recipe.Id.Value} (fix: pass the address of a {recipe} instance)");
    }
}
