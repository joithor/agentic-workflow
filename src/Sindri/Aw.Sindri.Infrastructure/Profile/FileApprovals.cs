using System.Text;
using System.Text.Json;
using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Infrastructure.Profile;

/// <summary>
/// Approved snapshots under &lt;home&gt;/approved: one directory per hash holding the canonical profile.json, and a
/// 'latest' file naming the most recent hash. A snapshot whose content no longer matches its hash isn't trusted.
/// </summary>
internal sealed class FileApprovals(SindriSettings settings, JsonSerializerOptions options) : IApprovals
{
    private readonly string _latestPath = Path.Combine(settings.ApprovalsDirectory, "latest");

    public async ValueTask<Result<LoadedProfile?>> Latest(CancellationToken cancellationToken)
    {
        if (!File.Exists(_latestPath))
        {
            return Results.Success<LoadedProfile?>(value: null);
        }

        var hashText = (await File.ReadAllTextAsync(_latestPath, cancellationToken).ConfigureAwait(false)).Trim();
        if (!ProfileHash.TryFrom(hashText, out var hash))
        {
            return SindriErrors.ProfileNotApproved.With($"{_latestPath} doesn't name an approval (fix: sindri profile approve)");
        }

        var snapshot = SnapshotPath(hash);
        if (!File.Exists(snapshot))
        {
            return SindriErrors.ProfileNotApproved.With($"{snapshot} is missing (fix: sindri profile approve)");
        }

        var loaded = ProfileReading.FromJson(await File.ReadAllBytesAsync(snapshot, cancellationToken).ConfigureAwait(false), snapshot, options);
        if (loaded.IsError || loaded.Value.Hash != hash)
        {
            return SindriErrors.ProfileNotApproved.With($"{snapshot} doesn't match its approval hash (fix: sindri profile approve)");
        }

        return loaded.Value;
    }

    public ValueTask<Result<Unit>> Save(LoadedProfile profile, CancellationToken cancellationToken)
    {
        OwnerOnly.EnsureDirectory(settings.Home);
        OwnerOnly.EnsureDirectory(settings.ApprovalsDirectory);
        OwnerOnly.EnsureDirectory(Path.GetDirectoryName(SnapshotPath(profile.Hash))!);   // why: a snapshot path always has a parent
        OwnerOnly.WriteAtomically(SnapshotPath(profile.Hash), ProfileReading.Canonical(profile.Profile, options));
        OwnerOnly.WriteAtomically(_latestPath, Encoding.UTF8.GetBytes(profile.Hash.Value + "\n"));
        return ValueTask.FromResult<Result<Unit>>(Unit.Value);
    }

    private string SnapshotPath(ProfileHash hash) => Path.Combine(settings.ApprovalsDirectory, hash.Value, "profile.json");
}
