using System.Globalization;
using Aw.Kernel;
using Aw.Sindri.Core.Ids;

namespace Aw.Sindri.Core.Profile;

/// <summary>The checks the JSON shape can't express: version, required text, service names and cadences.</summary>
public static class ProfileRules
{
    /// <summary>Checks <paramref name="profile"/> and returns it with defaults applied (sorted services and repos).</summary>
    /// <param name="profile">The deserialized profile.</param>
    /// <returns>The canonical profile, or SND-PROFILE-002 naming the key path and the fix.</returns>
    public static Result<SindriProfile> Check(SindriProfile profile)
    {
        if (profile.SchemaVersion != SindriProfile.CurrentSchemaVersion)
        {
            return Invalid("schemaVersion", string.Create(CultureInfo.InvariantCulture,
                $"is {profile.SchemaVersion}; this build reads {SindriProfile.CurrentSchemaVersion}"));
        }

        if (string.IsNullOrWhiteSpace(profile.User) || string.IsNullOrWhiteSpace(profile.Tenant))
        {
            return Invalid(string.IsNullOrWhiteSpace(profile.User) ? "user" : "tenant", "must be non-empty text");
        }

        if (profile.Api is { Port: < 0 or (> 0 and < 1024) or > 65535 })
        {
            return Invalid("api.port", "must be 0 (a free port) or 1024 to 65535");
        }

        foreach (var (name, settings) in profile.Services ?? SindriProfile.NoServices)
        {
            if (!ServiceName.TryFrom(name, out _))
            {
                return Invalid($"services.{name}", "is not a service name: kebab-case, 1 to 40 characters");
            }

            if (Cadence.Parse(settings.Every) is { IsSuccess: false } bad)
            {
                return Invalid($"services.{name}.every", bad.Error.Message);
            }
        }

        return Canonical(profile);
    }

    /// <summary>
    /// Whether the profile needs a (new) approval: nothing is approved, or the live profile reads and its hash differs.
    /// A live profile that doesn't read leaves the approval standing; its error is reported on its own.
    /// </summary>
    /// <param name="approved">The latest approval's hash, or null.</param>
    /// <param name="live">The live profile's hash, or null when it doesn't read.</param>
    /// <returns>True when approving is needed.</returns>
    public static bool NeedsApproval(ProfileHash? approved, ProfileHash? live) =>
        approved is null || (live is not null && live != approved);

    private static SindriProfile Canonical(SindriProfile profile) => profile with
    {
        Repos = [.. (profile.Repos ?? []).OrderBy(r => r.Name, StringComparer.Ordinal)],
        Services = new SortedDictionary<string, ServiceSettings>(
            (profile.Services ?? SindriProfile.NoServices).ToDictionary(StringComparer.Ordinal), StringComparer.Ordinal),
        Api = profile.Api ?? new ApiSettings(),
    };

    private static Error Invalid(string keyPath, string problem) =>
        SindriErrors.ProfileInvalid.With($"{keyPath}: {problem}");
}
