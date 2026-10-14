using Aw.Context.Core.Map;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>GET /sindri/context/map: the current map version and its counts, scoped for a session (context spec §8.5).</summary>
public sealed record GetContextMapRequest() : IQuery<MapStatusView>;
