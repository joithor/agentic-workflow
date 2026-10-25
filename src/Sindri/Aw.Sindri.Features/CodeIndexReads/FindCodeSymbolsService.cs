using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>A symbol search over the caller's repos (a session's is its own).</summary>
internal sealed class FindCodeSymbolsService(CodeIndexScopes scopes, ICodeIndex index) : IService<FindCodeSymbolsRequest, FindSymbolsAnswer>
{
    public async ValueTask<Result<FindSymbolsAnswer>> Handle(FindCodeSymbolsRequest request, CancellationToken cancellationToken)
    {
        var repo = CodeIndexInputs.Repo(request.Repo);
        var match = CodeIndexInputs.Match(request.Match);
        if (repo.IsError || match.IsError || string.IsNullOrWhiteSpace(request.Query))
        {
            return repo.IsError ? repo.Error : match.IsError ? match.Error : CodeIndex.Core.CodeIndexErrors.QueryInvalid.With("a symbol query is needed (fix: sindri code-index symbols <name>)");
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var answer = await index.FindSymbols(scope.Value, new SymbolQuery(request.Query, match.Value, repo.Value), cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : CodeIndexEgress.For(answer.Value, scope.Value.Caller.Kind == CallerKind.Session);
    }
}
