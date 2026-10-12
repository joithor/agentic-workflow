using Aw.Context.Core.Facade;
using Aw.Context.Core.Results;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The context sources' state through the engine, in the current caller's scope.</summary>
internal sealed class GetContextSourcesService(ContextScopes scopes, IContextEngine engine) : IService<GetContextSourcesRequest, SourcesView>
{
    public async ValueTask<Result<SourcesView>> Handle(GetContextSourcesRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Sources(scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
