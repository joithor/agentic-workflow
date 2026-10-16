using Aw.Context.Core.Proposals;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Where a scoping run's surfaces stand: mapped, new or unmapped (GET /sindri/context/map/proposals?run=).</summary>
/// <param name="Run">The scoping run's id.</param>
public sealed record GetContextProposalsRequest(string Run) : IQuery<ProposalsView>;
