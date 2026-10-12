using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary><see cref="IFactStore"/> as owner-only files under the home (red: engine P4 unit (c)).</summary>
internal sealed class HomeFactStore : IFactStore
{
    public HomeFactStore(WorkflowsHome home)
    {
        ArgumentNullException.ThrowIfNull(home);
    }

    public ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: engine P4 unit (c)");
}
