using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Prove;

internal sealed class ProveRerunScriptsStep(Aw.Sindri.Core.Prove.IProofScripts scripts, IRunArea area) : IBlockStep, IBlockStepId
{
    public static BlockId Id { get; } = BlockId.From("step.prove.rerun-scripts");

    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException();
}
