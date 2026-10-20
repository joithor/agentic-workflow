using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.GetSetup;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class GetSetupService : IService<GetSetupRequest, GetSetupResponse>
{
    public ValueTask<Result<GetSetupResponse>> Handle(GetSetupRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("GetSetup: make GetSetupTests pass");
}
