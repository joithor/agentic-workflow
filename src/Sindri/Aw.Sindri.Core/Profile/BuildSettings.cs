using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile's 'workflow.build:' block (line spec §7.2, LN-WF9): what the build and ship stations run in the phase worktree.</summary>
/// <param name="Commands">The repo's commands (<see cref="BuildCommand"/>); none: the stations run no command and say so in their findings.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<BuildCommand>? Commands = null);
