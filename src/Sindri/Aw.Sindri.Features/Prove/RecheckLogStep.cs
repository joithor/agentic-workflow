using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

internal sealed class RecheckLogStep(IRunArea area) : IBlockStep, IBlockStepId
{
    public static BlockId Id { get; } = BlockId.From("step.recheck.log");

    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
