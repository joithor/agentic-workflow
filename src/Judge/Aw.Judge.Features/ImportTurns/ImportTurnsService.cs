using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportTurns;

internal sealed class ImportTurnsService : IService<ImportTurnsRequest, ImportTurnsResponse>
{
    public ValueTask<Result<ImportTurnsResponse>> Handle(ImportTurnsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ImportTurns: make ImportTurnsTests pass (cutover unit 6.3)");
}
