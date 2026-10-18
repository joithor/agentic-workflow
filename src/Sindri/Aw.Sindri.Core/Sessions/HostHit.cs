namespace Aw.Sindri.Core.Sessions;

/// <summary>The first ancestor that names a host.</summary>
/// <param name="Kind">What it is.</param>
/// <param name="Name">The terminal id, the IDE id, or the kind's own name.</param>
/// <param name="Process">The process.</param>
/// <param name="Shell">The process just below it in the chain (the shell under a terminal), or null when the agent's parent is the host itself.</param>
public sealed record HostHit(HostKind Kind, string Name, ProcessEntry Process, ProcessEntry? Shell);
