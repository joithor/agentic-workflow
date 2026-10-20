using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>
/// 'step.scope.gather', on the block runtime: <c language="text">ScopingSteps.Gather</c>'s code behind <see cref="IBlockStep"/>. The context engine's views
/// are not facts but its Step inputs' typed results ('input.context.lenses', '.code' and '.history'), which the code reads as lensesView, codeView and historyView.
/// </summary>
internal sealed class ScopeGatherStep() : ScopingCodeStep(Id), IBlockStepId, IRepoStep
{
    private static readonly (string Input, string View)[] _views =
    [
        ("input.context.lenses", "lensesView"), ("input.context.code", "codeView"), ("input.context.history", "historyView"),
    ];

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.scope.gather");

    /// <inheritdoc/>
    protected override ValueTask<Result<IReadOnlyDictionary<string, string>>> Extra(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.InputResults.ContainsKey(_views[0].Input))
        {
            // As a block the lenses come from its input, not a --lenses file: say which input gave nothing, and where its section says why.
            return ValueTask.FromResult<Result<IReadOnlyDictionary<string, string>>>(SindriErrors.InvalidInput.With(
                "gather needs the lenses view, and input.context.lenses gave no typed result: the lenses were unavailable or their typed result was over 256 KiB (its injected section says which)"));
        }

        return ValueTask.FromResult(Facts(_views.Where(v => context.InputResults.ContainsKey(v.Input)).ToDictionary(static v => v.View, v => Text(context.InputResults[v.Input]), StringComparer.Ordinal)));
    }
}
