using Aw.Kernel;
using Aw.Sindri.Core.ModelHistory;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Tests.PickReads;

/// <summary>The model history of the pick tests: every instance runs recipe 'workflow.step' version 1, and the rows are the ones given (a model with none has an empty row).</summary>
/// <param name="rows">The rows, by state and model.</param>
internal sealed class FakeModelHistory(params ModelHistoryRow[] rows) : IModelHistory
{
    public static RecipeId Recipe { get; } = RecipeId.From("workflow.step");

    public ValueTask<Result<IReadOnlyList<ModelHistoryRow>>> For(RecipeId recipe, int version, string state, IReadOnlyList<string> models, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<IReadOnlyList<ModelHistoryRow>>>(Results.Success<IReadOnlyList<ModelHistoryRow>>([.. models.Select(m =>
            rows.FirstOrDefault(r => r.State == state && r.Model == m) ?? ModelHistoryRow.Empty(recipe, version, state, m))]));

    public ValueTask<Result<RunScope?>> ScopeOf(WorkflowInstanceId instance, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<RunScope?>>(Results.Success<RunScope?>(new RunScope(Recipe, 1)));
}
