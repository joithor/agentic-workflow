using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Blocks.Evidence;

/// <summary>The evidence providers this build ships (DT spec §5.5, §8.7): descriptors only; the runtime is DT-B's.</summary>
internal static class EvidenceProviders
{
    /// <summary>'evidence.check': re-runs the reproduction 'fact:check' and reports whether it passes.</summary>
    public static BlockDescriptor Check { get; } = new(
        BlockId.From("evidence.check"), BlockKind.EvidenceProvider, Version: 1,
        Inputs: [FactKey.From("fact:check")], Outputs: [], Outcomes: [], Capability.Tools, Parameters: [], Rung: null, DeskCapable: false);
}
