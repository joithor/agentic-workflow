using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Search;
using Aw.Context.Features.GetConceptMembers;
using Aw.Context.Features.GetConcepts;
using Aw.Context.Features.SearchMap;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The map search and the concept reads of the facade: each dispatches to its slice. Grouped so ContextCodeHistory stays within the dependency limit.</summary>
internal sealed class ContextMapReads(
    IService<SearchMapRequest, MapSearchView> search,
    IService<GetConceptsRequest, ConceptsView> concepts,
    IService<GetConceptMembersRequest, ConceptMembersView> members)
{
    public ValueTask<Result<MapSearchView>> Search(MapSearchQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        search.Handle(new SearchMapRequest(query, scope), cancellationToken);

    public ValueTask<Result<ConceptsView>> Concepts(ConceptsQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        concepts.Handle(new GetConceptsRequest(query, scope), cancellationToken);

    public ValueTask<Result<ConceptMembersView>> ConceptMembers(ConceptMembersQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        members.Handle(new GetConceptMembersRequest(query, scope), cancellationToken);
}
