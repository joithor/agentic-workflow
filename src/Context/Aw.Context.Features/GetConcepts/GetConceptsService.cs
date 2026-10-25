using Aw.Context.Core.Concepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.GetConcepts;

/// <summary>One page of the concepts of the map the caller may read.</summary>
internal sealed class GetConceptsService : IService<GetConceptsRequest, ConceptsView>
{
    public ValueTask<Result<ConceptsView>> Handle(GetConceptsRequest request, CancellationToken cancellationToken) => throw new NotSupportedException();
}
