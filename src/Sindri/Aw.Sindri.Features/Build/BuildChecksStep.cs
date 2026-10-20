using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Packets;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>'step.build.checks' on the block runtime (line spec §7.2): the scope gate and the BLD-* rules over the measured diff, then the repo's commands and the red tests on the head.</summary>
internal sealed class BuildChecksStep : IBlockStep, IBlockStepId
{
    public BuildChecksStep(IRunDiff diff, IBranchChanges changes, IPacketFiles files, RepoCommandRunner commands, ITextScrubber scrubber)
    {
        _ = (diff, changes, files, commands, scrubber);
    }

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.checks");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
