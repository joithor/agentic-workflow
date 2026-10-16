using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.DbRun;

/// <summary>Runs a database script read-only and writes its summary.</summary>
internal sealed class DbRunService : IService<DbRunRequest, DbRunResponse>
{
    public ValueTask<Result<DbRunResponse>> Handle(DbRunRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("DbRunService is not written yet");
}
