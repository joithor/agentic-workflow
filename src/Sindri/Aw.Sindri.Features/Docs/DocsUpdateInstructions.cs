using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Docs;

/// <summary>What 'step.docs.update' is told (PR babysitter spec Appendix A.4): the seed's goal, how to work and what never to do, as lines the launch puts after the brief's first line.</summary>
internal sealed class DocsUpdateInstructions : IAgentStepInstructions
{
    private static readonly BlockId _step = BlockId.From("step.docs.update");

    /// <inheritdoc/>
    public bool IsFor(BlockId step) => step == _step;

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException("red: not written yet");
}
