using Aw.Context.Core.Map;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/map: the current map version and its counts, scoped for a session (context spec §8.5).</summary>
/// <param name="Anchor">A node key, name or alias: the answer then also carries the coverage of its own scope (the lenses' record), or null for none.</param>
public sealed record GetContextMapRequest(string? Anchor = null) : IQuery<MapStatusView>;
