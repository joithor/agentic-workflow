using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core.Commands;
using Aw.Sindri.Core.Items;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Answers a context question through the engine, in the current caller's scope.</summary>
internal sealed class QueryContextService(ContextScopes scopes, IContextEngine engine, IItemResolver items, ICallerContextAccessor callers) : IService<QueryContextRequest, ContextResult>
{
    public async ValueTask<Result<ContextResult>> Handle(QueryContextRequest request, CancellationToken cancellationToken)
    {
        var depth = QueryRules.ParseDepth(request.Depth);
        var budget = QueryRules.ParseBudget(request.Budget);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        var item = await ItemKeys.Resolve(request.Item, items, callers.Current, cancellationToken).ConfigureAwait(false);
        return item.IsError ? item.Error
            : depth.IsError ? depth.Error
            : budget.IsError ? budget.Error
            : scope.IsError ? scope.Error
            : await engine.Query(new ContextQuery(request.Q, request.Node, item.Value, request.Symbol, depth.Value, budget.Value, request.Draft),
                scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
