using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Evidence;

/// <summary>What an evidence agent Step is told (cutover spec §3.1, §3.4): the ui-evidence skill's verb lines for that Step, as lines the launch puts after the brief's first line.</summary>
internal sealed class EvidenceStepInstructions : IAgentStepInstructions
{
    private readonly BlockId _step;

    private EvidenceStepInstructions(string step, IReadOnlyList<string> lines)
    {
        _step = BlockId.From(step);
        Lines = lines;
    }

    /// <summary>The instructions of every evidence agent Step that has any.</summary>
    internal static IReadOnlyList<EvidenceStepInstructions> All { get; } = [];

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines { get; }

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;
}
