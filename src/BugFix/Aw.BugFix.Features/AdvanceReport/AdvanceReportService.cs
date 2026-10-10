using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.BugFix.Features.AdvanceReport;

internal sealed class AdvanceReportService : IService<AdvanceReportRequest, AdvanceReportResponse>
{
    public ValueTask<Result<AdvanceReportResponse>> Handle(AdvanceReportRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("AdvanceReport: make AdvanceReportTests pass");
}
