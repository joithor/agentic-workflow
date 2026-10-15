using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.GetInstanceFact;

/// <summary>The scrubbed body behind one fact row (API spec section 3.8, GET /workflow/instances/{address}/facts/{key}; query).</summary>
/// <param name="Instance">The instance address or id.</param>
/// <param name="Key">The fact key.</param>
public sealed record GetInstanceFactRequest(string Instance, string Key) : IQuery<FactView>;
