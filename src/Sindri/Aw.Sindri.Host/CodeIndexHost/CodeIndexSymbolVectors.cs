using Aw.Context.Core.Engine;
using Aw.Context.Core.Glossary;
using Aw.Kernel;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>Context's <see cref="ISymbolVectors"/> over the code index facade.</summary>
/// <param name="index">The code index.</param>
/// <param name="scopes">The scope the index is read in.</param>
internal sealed class CodeIndexSymbolVectors(Aw.CodeIndex.Core.Facade.ICodeIndex index, ICodeIndexScopeSource scopes) : ISymbolVectors
{
    public ValueTask<Result<SymbolVectorsRead>> Read(ContextRepoName repo, IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        _ = (index, scopes);
        throw new NotSupportedException();
    }
}
