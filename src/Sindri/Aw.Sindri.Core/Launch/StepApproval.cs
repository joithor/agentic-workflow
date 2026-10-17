using Aw.Kernel;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Core.Launch;

/// <summary>SL8: an unattended Step runs only approved argv. Pure.</summary>
public static class StepApproval
{
    /// <summary>Checks <paramref name="entry"/> for an unattended Step.</summary>
    /// <param name="entry">The loaded entry; the caller builds the argv from the entry this returns, never from a re-read.</param>
    /// <param name="builtinArgvHash">The built-in entry's hash for the same id, or null when there is no built-in of that id.</param>
    /// <param name="approved">The approved profile's 'agents:' block, or null.</param>
    /// <returns>The same entry; SND-LAUNCH-012 with 'reason', 'current' and 'approved' when the argv isn't approved.</returns>
    public static Result<AgentEntry> Check(AgentEntry entry, string? builtinArgvHash, AgentSettings? approved) =>
        throw new NotSupportedException();

    /// <summary>Resolves <paramref name="id"/> once and checks the entry it got.</summary>
    /// <param name="catalog">The catalog.</param>
    /// <param name="id">The agent id.</param>
    /// <param name="builtinArgvHash">The built-in entry's hash for <paramref name="id"/>, or null.</param>
    /// <param name="approved">The approved profile's 'agents:' block, or null.</param>
    /// <returns>The entry; SND-LAUNCH-003 'not-in-catalog'; or what <see cref="Check"/> returns.</returns>
    public static Result<AgentEntry> Resolve(IAgentCatalog catalog, AgentId id, string? builtinArgvHash, AgentSettings? approved) =>
        throw new NotSupportedException();
}
