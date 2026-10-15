using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OwnerAnswer;

internal sealed class AnswerOwnerService : IService<AnswerOwnerRequest, AnswerOwnerResponse>
{
    public ValueTask<Result<AnswerOwnerResponse>> Handle(AnswerOwnerRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
