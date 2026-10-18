using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.RecordEval;

/// <summary>'recipe eval --record': reads an instance's events from the ledger and writes them back as an eval case (engine P7, owner answer Q6).</summary>
internal sealed class RecordEvalService : IService<RecordEvalRequest, RecordEvalResponse>
{
    public ValueTask<Result<RecordEvalResponse>> Handle(RecordEvalRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("engine P7 U6");
}
