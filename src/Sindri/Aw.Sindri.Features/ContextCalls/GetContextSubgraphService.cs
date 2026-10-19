using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Core.Map;
using Aw.Context.Core.Subgraph;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The subgraph of a set of node keys through the engine, for the caller's scope; the one service behind GET and QUERY.</summary>
internal sealed class GetContextSubgraphService(ContextScopes scopes, IContextEngine engine) : IService<GetContextSubgraphRequest, SubgraphView>
{
    public async ValueTask<Result<SubgraphView>> Handle(GetContextSubgraphRequest request, CancellationToken cancellationToken)
    {
        var nodes = request.Nodes ?? [];
        if (request.FromUrl && QueryRules.CheckSubgraphGet(nodes) is { } refused)
        {
            return refused;
        }

        var read = MapRead.Of(draft: false, request.MapVersion);
        if (read.IsError)
        {
            return read.Error;
        }

        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Subgraph(new SubgraphQuery(nodes, request.Depth, read.Value), scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
