using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.ImportLabels;

internal sealed class ImportLabelsService : IService<ImportLabelsRequest, ImportLabelsResponse>
{
    public ValueTask<Result<ImportLabelsResponse>> Handle(ImportLabelsRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ImportLabels: make ImportLabelsTests pass (cutover unit 6.3)");
}
