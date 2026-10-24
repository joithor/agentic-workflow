using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Step;

/// <summary>The relevance review's three Steps (issue 966 spec 4.1 to 4.3).</summary>
internal static class RelevanceSteps
{
    /// <summary>'step.scope.relevance-review': an agent labels every returned entry in, out or noise.</summary>
    public static BlockDescriptor Review => throw new NotSupportedException();

    /// <summary>'step.scope.relevance-judge': a judge labels the same entries without seeing the agent's labels.</summary>
    public static BlockDescriptor Judge => throw new NotSupportedException();

    /// <summary>'step.scope.relevance-apply': drops what both call off topic and keeps what they dispute.</summary>
    public static BlockDescriptor Apply => throw new NotSupportedException();
}
