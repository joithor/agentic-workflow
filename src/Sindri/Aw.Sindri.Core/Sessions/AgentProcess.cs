using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Sessions;

/// <summary>A process that runs an agent CLI (collector spec §2).</summary>
/// <param name="Process">The process.</param>
/// <param name="Agent">The catalog entry it matches.</param>
public sealed record AgentProcess(ProcessEntry Process, AgentEntry Agent);
