using Aw.Context.Core.Code;
using Aw.Context.Core.Facade;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>The code lens of some anchors, read from one map snapshot, through the engine (a session sees only its repo's nodes).</summary>
internal sealed class GetContextCodeService(ContextScopes scopes, IContextEngine engine) : IService<GetContextCodeRequest, CodeView>
{
    public async ValueTask<Result<CodeView>> Handle(GetContextCodeRequest request, CancellationToken cancellationToken)
    {
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        return scope.IsError ? scope.Error : await engine.Code(request.Anchors, request.Draft, request.MapVersion, scope.Value, cancellationToken).ConfigureAwait(false);
    }
}
