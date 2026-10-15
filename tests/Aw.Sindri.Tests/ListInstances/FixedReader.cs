using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Tests.ListInstances;

internal sealed class FixedReader(IReadOnlyList<InstanceView> views) : IInstanceReader
{
    public ValueTask<Result<InstanceView>> Get(InstanceAddress address, CancellationToken cancellationToken) =>
        ValueTask.FromResult<Result<InstanceView>>(views.First(v => v.Address == address));
}
