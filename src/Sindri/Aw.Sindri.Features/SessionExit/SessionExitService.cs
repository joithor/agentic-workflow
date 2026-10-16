using System.Diagnostics.CodeAnalysis;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.SessionExit;

[SuppressMessage("Design", "MA0182:Avoid unused internal types", Justification = "--slice-only: unused until this branch registers the slice; remove with that registration")]
internal sealed class SessionExitService : IService<SessionExitRequest, SessionExitResponse>
{
    public ValueTask<Result<SessionExitResponse>> Handle(SessionExitRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("SessionExit: make SessionExitTests pass");
}
