using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The callers of a symbol in the caller's scope; a ref in another repo is the same not-found as one never built.</summary>
internal sealed class GetCodeCallersService(CodeIndexScopes scopes, ICodeIndex index) : IService<GetCodeCallersRequest, CodeCallsView>
{
    public async ValueTask<Result<CodeCallsView>> Handle(GetCodeCallersRequest request, CancellationToken cancellationToken)
    {
        var symbol = CodeIndexInputs.Symbol(request.Ref, request.Repo);
        var depth = CodeIndexInputs.Depth(request.Depth);
        if (symbol.IsError || depth.IsError)
        {
            return symbol.IsError ? symbol.Error : depth.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError)
        {
            return scope.Error;
        }

        var answer = await index.Callers(scope.Value, symbol.Value, depth.Value, request.Implementations, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : CodeIndexEgress.For(CodeCallsView.From(answer.Value), scope.Value.Caller.Kind == CallerKind.Session);
    }
}
