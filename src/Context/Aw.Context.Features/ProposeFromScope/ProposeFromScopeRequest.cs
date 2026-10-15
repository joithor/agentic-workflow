using Aw.Context.Core.Engine;
using Aw.Context.Core.Proposals;

namespace Aw.Context.Features.ProposeFromScope;

/// <summary>Writes a scoping run's surfaces as 'propose' rows in the latest draft.</summary>
internal sealed record ProposeFromScopeRequest(ScopeProposal Proposal, ContextScope Scope);
