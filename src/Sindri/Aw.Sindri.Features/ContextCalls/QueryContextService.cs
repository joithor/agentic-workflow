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
        var depth = QueryRules.ParseDepth(request.Depth);
        var budget = QueryRules.ParseBudget(request.Budget);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return depth.IsError ? depth.Error
            : budget.IsError ? budget.Error
            : scope.IsError ? scope.Error
            : await engine.Query(new ContextQuery(request.Q, request.Node, request.Item, request.Symbol, depth.Value, budget.Value, request.Draft),
                scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
