using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Answers a context question through the engine, in the current caller's scope.</summary>
internal sealed class QueryContextService(ContextScopes scopes, IContextEngine engine) : IService<QueryContextRequest, ContextResult>
{
    public async ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError
            ? scope.Error
            : await engine.Query(new ContextQuery(request.Q, request.Node, request.Item, request.Symbol, request.Depth, request.Budget, request.Draft),
                scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
