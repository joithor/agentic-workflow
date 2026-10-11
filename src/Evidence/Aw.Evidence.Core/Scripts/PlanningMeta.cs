using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Scripts;

/// <summary>The planner's one model invocation that wrote the script. Null means unknown, never zero.</summary>
/// <param name="Pr">The pull request the script was planned for.</param>
/// <param name="Model">The planning model.</param>
/// <param name="ElapsedMs">How long planning took.</param>
/// <param name="InputTokens">Input tokens, when reported.</param>
/// <param name="OutputTokens">Output tokens, when reported.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record PlanningMeta(string? Pr = null, string? Model = null, long? ElapsedMs = null, long? InputTokens = null, long? OutputTokens = null);
