using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.CompleteStep;

/// <summary>Engine P4 unit (c): stores the outputs, checks the nonce, and sends the 'complete' Update (red).</summary>
internal sealed class CompleteStepService : IService<CompleteStepRequest, CompleteStepResponse>
{
    public ValueTask<Result<CompleteStepResponse>> Handle(CompleteStepRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: engine P4 unit (c), CompleteStep");
}
