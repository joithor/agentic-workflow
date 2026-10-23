namespace Aw.Sindri.Core.EngineReads;

/// <summary>A timer an instance has running (API spec §3.8, E-30).</summary>
/// <param name="Name">The timer's name.</param>
/// <param name="FiresAt">When it fires.</param>
/// <param name="Kind">'timeout' for a deadline.</param>
public sealed record EngineTimer(string Name, DateTimeOffset FiresAt, string Kind);
