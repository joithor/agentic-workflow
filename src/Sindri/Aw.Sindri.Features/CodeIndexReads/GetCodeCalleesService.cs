using Aw.CodeIndex.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>The callees of a symbol in the caller's scope.</summary>
internal sealed class GetCodeCalleesService(CodeIndexScopes scopes, ICodeIndex index) : IService<GetCodeCalleesRequest, CodeCallsView>
{
    public async ValueTask<Result<CodeCallsView>> Handle(GetCodeCalleesRequest request, CancellationToken cancellationToken)
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

        var answer = await index.Callees(scope.Value, symbol.Value, depth.Value, cancellationToken).ConfigureAwait(false);
        return answer.IsError ? answer.Error : CodeIndexEgress.For(CodeCallsView.From(answer.Value), scope.Value.Caller.Kind == CallerKind.Session);
    }
}
