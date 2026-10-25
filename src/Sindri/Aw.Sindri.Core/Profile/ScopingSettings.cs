using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.scoping:' block (the scoping recipe's keys).</summary>
/// <param name="HistoryWindow">The window the scoping recipe's history input reads (<see cref="HistoryWindowSettings"/>); the recipe names this key, so a profile without it is refused at the input (SND-WF-021).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingSettings(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] HistoryWindowSettings? HistoryWindow = null);
