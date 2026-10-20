using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Line;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Features.Design;

/// <summary>The server-side read model of a workflow.plan instance (line spec LN-API2): its facts, read as the fact route reads them (scrubbed), go through the same builder as 'plan show --facts'.</summary>
internal sealed class GetInstanceDesignStopService : IService<GetInstanceDesignStopRequest, DesignStopView>
{
    /// <summary>Holds what the read needs.</summary>
    /// <param name="instances">Opens the instance for the caller.</param>
    /// <param name="contents">The stored fact content.</param>
    /// <param name="scrubber">Removes secrets from the text.</param>
    public GetInstanceDesignStopService(PlanInstances instances, IFactContents contents, ITextScrubber scrubber)
    {
        ArgumentNullException.ThrowIfNull(instances);
        ArgumentNullException.ThrowIfNull(contents);
        ArgumentNullException.ThrowIfNull(scrubber);
    }

    public ValueTask<Result<DesignStopView>> Handle(GetInstanceDesignStopRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
