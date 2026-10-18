using System.Text.Json.Serialization;

namespace Aw.Sindri.Core.Profile;

/// <summary>
/// The profile's 'workflow.route:' block (engine P7 plan D8, workflow engine spec §12): what the router stub does with an
/// item it routes. Real routing replaces it; until then 'gate.route.startable' reads this.
/// </summary>
/// <param name="Startable">'all' (the default) lets every routed item start; 'none' stops them at the router.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record RouteSettings([property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Startable = null)
{
    /// <summary>Every routed item may start (the default).</summary>
    public const string All = "all";

    /// <summary>No routed item may start.</summary>
    public const string None = "none";

    /// <summary>Whether <paramref name="profile"/> lets routed items start: true unless 'workflow.route.startable' is 'none'.</summary>
    /// <param name="profile">A checked profile.</param>
    /// <returns>True when items start.</returns>
    public static bool StartableOf(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return !string.Equals(profile.Workflow?.Route?.Startable, None, StringComparison.Ordinal);
    }
}
