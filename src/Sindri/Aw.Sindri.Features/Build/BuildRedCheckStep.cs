using Aw.Sindri.Core.Checks;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Build;

/// <summary>'step.build.red-check' on the block runtime (line spec §7.2, BLD-RED-001): every red test of every item must fail on the red commit.</summary>
internal sealed class BuildRedCheckStep : IBlockStep, IBlockStepId
{
    /// <summary>The longest one command may run when the profile sets no timeout for it.</summary>
    internal static readonly TimeSpan CommandTimeout = TimeSpan.FromMinutes(40);

    public BuildRedCheckStep(IRunDiff diff, RepoCommandRunner commands)
    {
        _ = (diff, commands);
    }

    /// <inheritdoc/>
    public static BlockId Id { get; } = BlockId.From("step.build.red-check");

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken) => throw new NotSupportedException("red");
}
