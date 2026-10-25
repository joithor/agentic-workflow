using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConceptMembers;

/// <summary>One page of one concept's members in the map the caller may read.</summary>
internal sealed class GetConceptMembersService : IService<GetConceptMembersRequest, ConceptMembersView>
{
    public ValueTask<Result<ConceptMembersView>> Handle(GetConceptMembersRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
