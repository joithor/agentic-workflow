using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.Kernel;

namespace Aw.CodeIndex.Infrastructure.Store;

internal sealed partial class SqliteIndexReader
{
    public ValueTask<Result<IReadOnlyList<SymbolVector>>> SymbolVectors(IReadOnlyList<string> paths, string model, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
