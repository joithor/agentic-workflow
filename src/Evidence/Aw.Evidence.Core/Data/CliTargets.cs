using System.Text.Json.Serialization;

namespace Aw.Evidence.Core.Data;

/// <summary>The 'cli' part of the targets config.</summary>
/// <param name="BuildOutputs">Directories relative to the repo whose executables a script may run.</param>
/// <param name="Allow">Absolute paths of programs a script may run.</param>
/// <param name="PassEnv">Variables every CLI call may inherit.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CliTargets(
    IReadOnlyList<string>? BuildOutputs = null,
    IReadOnlyList<string>? Allow = null,
    IReadOnlyList<string>? PassEnv = null);
