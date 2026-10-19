using System.Text.Json.Nodes;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Features.Blocks.Step;

namespace Aw.Sindri.Features;

/// <summary>'step.ship.return' on the block runtime (line spec §7.5): a red GREEN gate or a stale head goes back to build, and the findings become the notes the builder reads.</summary>
internal sealed class ShipReturnStep : IBlockStep, IDescribedBlock
{
    /// <inheritdoc/>
    public static BlockDescriptor Descriptor => ShipSteps.Return;

    /// <inheritdoc/>
    public ValueTask<BlockStepResult> Run(StepContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        if (StepFacts.Read(context, "fact:findings") is not { } findings)
        {
            return ValueTask.FromResult(BlockStepResult.Failed("step.ship.return needs fact:findings, which the instance does not hold"));
        }

        var notes = new JsonObject { ["from"] = "ship", ["findings"] = findings.DeepClone() };
        return ValueTask.FromResult(StepFacts.Done("noted", ("fact:returnNotes", notes)));
    }
}
