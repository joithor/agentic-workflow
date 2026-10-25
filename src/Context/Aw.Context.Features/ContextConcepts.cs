using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Context.Core.Facade;
using Aw.Context.Features.GetConceptMembers;
using Aw.Context.Features.GetConcepts;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features;

/// <summary>The concept layer's reads of the facade: each dispatches to its slice.</summary>
internal sealed class ContextConcepts(
    IService<GetConceptsRequest, ConceptsView> concepts,
    IService<GetConceptMembersRequest, ConceptMembersView> members) : IContextConcepts
{
    public ValueTask<Result<ConceptsView>> Concepts(ConceptsQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        concepts.Handle(new GetConceptsRequest(query, scope), cancellationToken);

    public ValueTask<Result<ConceptMembersView>> ConceptMembers(ConceptMembersQuery query, ContextScope scope, CancellationToken cancellationToken) =>
        members.Handle(new GetConceptMembersRequest(query, scope), cancellationToken);
}
