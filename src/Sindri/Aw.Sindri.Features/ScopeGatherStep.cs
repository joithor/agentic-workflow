using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>
/// 'step.scope.gather', on the block runtime: <see cref="ScopingSteps.Gather"/>'s code behind <see cref="IBlockStep"/>. The context engine's views
/// are not facts but its Step inputs' typed results ('input.context.lenses', '.code' and '.history'), which the code reads as lensesView, codeView and historyView.
/// </summary>
internal sealed class ScopeGatherStep() : ScopingCodeStep(Descriptor), IDescribedBlock
{
    private static readonly (string Input, string View)[] _views =
    [
        ("input.context.lenses", "lensesView"), ("input.context.code", "codeView"), ("input.context.history", "historyView"),
    ];

    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ScopingSteps.Gather;

    /// <inheritdoc/>
    protected override IReadOnlyDictionary<string, string> Extra(StepContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return _views.Where(v => context.InputResults.ContainsKey(v.Input)).ToDictionary(static v => v.View, v => Text(context.InputResults[v.Input]), StringComparer.Ordinal);
    }
}
