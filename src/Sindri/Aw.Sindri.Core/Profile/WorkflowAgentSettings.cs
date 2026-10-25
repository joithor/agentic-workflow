using System.Text.Json.Serialization;
using Aw.Sindri.Core.Launch;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.agent:' block (engine spec §6.5, launcher L-14): where a Step whose runtime is the agent runs.</summary>
/// <param name="Target">The agent a Step launches; an agent id checked by pattern only (the catalog is per box). Absent: no agent runtime, so a deskCapable Step runs at the desk and any other parks (SND-WF-010).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record WorkflowAgentSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] AgentId? Target = null);
