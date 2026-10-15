using Aw.Context.Core.Engine;

namespace Aw.Context.Features.GetProposals;

/// <summary>Where each surface a scoping run proposed stands.</summary>
internal sealed record GetProposalsRequest(string Run, ContextScope Scope);
