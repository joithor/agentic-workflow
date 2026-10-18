
namespace Aw.Sindri.Core.Sandbox.Plan;

/// <summary>The named host groups a session may reach (sandbox spec §3.2).</summary>
public static class HostGroups
{
    /// <summary>Resolves the groups of one launch: built-in, the profile's, the repo's, minus what the Step removes.</summary>
    /// <param name="input">What the groups depend on.</param>
    /// <returns>The groups.</returns>
    public static IReadOnlyList<PlanGroup> Resolve(GroupsInput input) => throw new NotSupportedException();

    /// <summary>Whether a host pattern ('api.nuget.org' or '*.nuget.org') matches a host.</summary>
    /// <param name="pattern">The pattern.</param>
    /// <param name="host">The host name.</param>
    /// <returns>True when it matches.</returns>
    public static bool Matches(string pattern, string host) => throw new NotSupportedException();

    /// <summary>The group that allows <paramref name="host"/>, or null.</summary>
    /// <param name="groups">The plan's groups.</param>
    /// <param name="host">The host name.</param>
    /// <returns>The group, or null.</returns>
    public static PlanGroup? Allowing(IReadOnlyList<PlanGroup> groups, string host) => throw new NotSupportedException();
}
