using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;

namespace Aw.CodeIndex.Features;

// The stored-vectors read: the embedded symbols of a set of files, for the context map's glossary signal.
public sealed partial class CodeIndexFacade
{
    /// <inheritdoc />
    public ValueTask<Result<SymbolVectorsView>> SymbolVectors(CodeIndexScope scope, RepoName repo, SymbolVectorsQuery query, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
