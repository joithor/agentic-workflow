using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What 'step.scope.draft' is told (issue 1005): work from the prefiltered graph and the context tools, and grep only to confirm.</summary>
internal sealed class ScopeDraftInstructions : IAgentStepInstructions
{
    /// <inheritdoc/>
    public bool IsFor(BlockId step) => throw new NotSupportedException();

    /// <inheritdoc/>
    public IReadOnlyList<string> Lines => throw new NotSupportedException();
}
