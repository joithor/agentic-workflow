using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.Owner;

/// <summary>Lists the questions queued for the owner: from lanes' "OWNER QUESTION" messages and the meta's own.</summary>
/// <param name="owner">The owner's questions.</param>
internal sealed class ListOwnerService(IOwnerStore owner) : IService<ListOwnerRequest, ListOwnerResponse>
{
    public async ValueTask<Result<ListOwnerResponse>> Handle(ListOwnerRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var questions = await owner.List(request.All, cancellationToken).ConfigureAwait(false);
        return questions.IsError ? questions.Error : new ListOwnerResponse(questions.Value);
    }
}
