using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// One of the repo's commands the build and ship stations run in the phase worktree (line spec §7.2, LN-WF9, Appendix D): a typecheck, the unit lane, a lint. A command is an argv
/// list, never a shell string; it runs with the phase worktree as its working directory.
/// </summary>
/// <param name="Name">Its name, kebab-case (for example 'typecheck'); unique among the commands, and what a finding names.</param>
/// <param name="Run">The program and its arguments. On 'red' the test ids of the item are appended.</param>
/// <param name="Timeout">The longest it may run, a duration ('30m', '2h'); absent: the Step's own bound.</param>
/// <param name="On">Where it runs: 'red' (build.red-check: the red tests, which must fail), 'checks' (build.checks, the default) or 'green' (ship.green).</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record BuildCommand(
    string Name,
    IReadOnlyList<string> Run,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Timeout = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? On = null)
{
    /// <summary>The red check's command: runs the item's red tests.</summary>
    public const string Red = "red";

    /// <summary>The build checks' command (the default).</summary>
    public const string Checks = "checks";

    /// <summary>The ship GREEN gate's command.</summary>
    public const string Green = "green";

    /// <summary>Where this command runs: <see cref="On"/>, or <see cref="Checks"/> when it names none.</summary>
    [JsonIgnore]
    public string Where => On ?? Checks;
}
