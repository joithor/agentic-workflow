using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Judge.Features.Adjudicate;

internal sealed class AdjudicateService : IService<AdjudicateRequest, AdjudicateResponse>
{
    public ValueTask<Result<AdjudicateResponse>> Handle(AdjudicateRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("Adjudicate: make AdjudicateTests pass (cutover unit 6.3)");
}
