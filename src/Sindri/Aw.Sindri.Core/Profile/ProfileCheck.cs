using Aw.Kernel;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>The profile watch's decision (API spec C8): whether the (approved, live) pair changed since the last 'profile.checked'.</summary>
public static class ProfileCheck
{
    /// <summary>
    /// The 'profile.checked' to record, or null when the (approved, live) pair is the one <paramref name="previous"/> saw.
    /// Drifted means a profile is approved and the live one differs (G9); an unreadable live profile is live null.
    /// </summary>
    /// <param name="previous">The last recorded check, or null before the first.</param>
    /// <param name="approved">The approved profile, or null when none is.</param>
    /// <param name="live">The live profile, or the error reading it.</param>
    /// <param name="commandId">The check's command id.</param>
    /// <returns>The payload to record, or null.</returns>
    public static ProfileChecked? Decide(ProfileChecked? previous, LoadedProfile? approved, Result<LoadedProfile> live, CommandId commandId)
    {
        ArgumentNullException.ThrowIfNull(live);
        var loaded = live.IsSuccess ? live.Value : null;
        var approvedHash = approved?.Hash;
        var liveHash = loaded?.Hash;
        if (previous is not null && previous.Approved == approvedHash && previous.Live == liveHash)
        {
            return null;
        }

        var drifted = approved is not null && liveHash != approvedHash;
        return new ProfileChecked(commandId, approvedHash, liveHash, drifted, drifted ? Paths(approved, loaded) : [], Services(approved));
    }

    private static IReadOnlyList<string> Paths(LoadedProfile? approved, LoadedProfile? live) =>
        approved is null || live is null ? [] : ProfileDiff.Between(approved.Profile, live.Profile);

    private static IReadOnlyList<ServiceName> Services(LoadedProfile? approved) =>
        approved is null ? [] : [.. (approved.Profile.Services ?? SindriProfile.NoServices).Keys.Order(StringComparer.Ordinal).Select(ServiceName.From)];
}
