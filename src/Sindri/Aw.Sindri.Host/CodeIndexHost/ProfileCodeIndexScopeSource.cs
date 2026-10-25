using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Sindri.Features.CodeIndexReads;

namespace Aw.Sindri.Host.CodeIndexHost;

/// <summary>The scope the read slices use (<see cref="CodeIndexScopes"/>), so Context reads the index with the same repos and the same session rule as the routes.</summary>
internal sealed class ProfileCodeIndexScopeSource(CodeIndexScopes scopes) : ICodeIndexScopeSource
{
    public ValueTask<Result<CodeIndexScope>> Current(CancellationToken cancellationToken) => scopes.Current(cancellationToken);
}
