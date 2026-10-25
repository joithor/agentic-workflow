using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The publish status of the map for the caller: a session sees its own repos' lines and no count of others (code index spec 6.4).</summary>
internal sealed class GetMapPublishStatusService(ContextScopes scopes, MapPublishStatusReader reader) : IService<GetMapPublishStatusRequest, MapPublishStatusView>
{
    public async ValueTask<Result<MapPublishStatusView>> Handle(GetMapPublishStatusRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await reader.Read(scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
