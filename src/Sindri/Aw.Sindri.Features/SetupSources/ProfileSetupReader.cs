using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Setup;

namespace Aw.Sindri.Features.SetupSources;

/// <summary>
/// What the setup list reads of the profile: the repos the live file names, and where it stands against its approval (a missing file is "no profile", a file with a mistake
/// is an error the list shows as that step's failure). The live file is judged as `sindri status` judges it, against the approved hash.
/// </summary>
/// <param name="live">The live profile file.</param>
/// <param name="approvals">The approved profile.</param>
internal sealed class ProfileSetupReader(IProfileSource live, IApprovedProfile approvals)
{
    internal async ValueTask<Result<RepoFact>> Repos(CancellationToken cancellationToken)
    {
        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            return loaded.Error.Code == SindriErrors.ProfileMissing.Code ? new RepoFact(0) : loaded.Error;
        }

        var repos = loaded.Value.Profile.Repos ?? [];
        return new RepoFact(repos.Count) { Items = new SetupItems([.. repos.Select(static r => new SetupItem(r.Name, SetupState.Done, Detail: null))]) };
    }

    internal async ValueTask<Result<ProfileFact>> Presence(CancellationToken cancellationToken)
    {
        var approved = await approvals.Latest(cancellationToken).ConfigureAwait(false);
        if (approved.IsError)
        {
            return approved.Error;
        }

        var loaded = await live.Load(cancellationToken).ConfigureAwait(false);
        if (loaded.IsError)
        {
            // like status: a live file that doesn't load is judged by the saved approval; a file with a mistake is the step's failure
            return loaded.Error.Code != SindriErrors.ProfileMissing.Code ? loaded.Error : new ProfileFact(approved.Value is null ? ProfilePresence.Missing : ProfilePresence.Approved);
        }

        if (approved.Value is not { } saved)
        {
            return new ProfileFact(ProfilePresence.Unapproved);
        }

        return new ProfileFact(string.Equals(saved.Hash.Value, loaded.Value.Hash.Value, StringComparison.Ordinal) ? ProfilePresence.Approved : ProfilePresence.Drifted);
    }
}
