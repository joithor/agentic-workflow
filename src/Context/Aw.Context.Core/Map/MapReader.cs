using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Map;

/// <summary>Who reads the map, under which settings: sessions are gated by 'context.sessions.enabled' and scoped to their repo (spec §3).</summary>
/// <param name="Settings">The profile's checked context settings.</param>
/// <param name="Caller">The caller.</param>
public sealed record MapReader(ContextSettings Settings, ContextCaller Caller);
