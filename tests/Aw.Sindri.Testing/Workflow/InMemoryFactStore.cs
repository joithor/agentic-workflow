using Aw.Kernel;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;

namespace Aw.Sindri.Testing.Workflow;

/// <summary>The in-memory <see cref="IFactStore"/> (red: engine P4 unit (c)).</summary>
public sealed class InMemoryFactStore : IFactStore
{
    /// <inheritdoc/>
    public ValueTask<Result<FactValue>> Put(ReadOnlyMemory<byte> content, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: engine P4 unit (c)");
}
