using Aw.Context.Core.Proposals;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ProposeFromScope;

/// <summary>Writes a scoping run's surfaces as 'propose' rows in the latest draft. (S-4 stub: red until its slice is built.)</summary>
internal sealed class ProposeFromScopeService : IService<ProposeFromScopeRequest, ProposeResult>
{
    public ValueTask<Result<ProposeResult>> Handle(ProposeFromScopeRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("S-4: ProposeFromScope is not built yet");
}
