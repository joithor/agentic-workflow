using Aw.Context.Core.Concepts;
using Aw.Context.Core.Engine;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Features;

/// <summary>The one mapping from the approved profile's repos to the entries the context engine takes, shared by every slice that builds a context scope (it belongs to no slice, AW1003).</summary>
internal static class ProfileRepoEntries
{
    /// <summary>
    /// The profile's repos as the engine takes them: each with the globs of the profile's 'index.denyPaths' and its own (extraction
    /// adds them to the built-in secret globs and never reads a path they match) and the pins of 'index.concepts', or none when neither names any.
    /// </summary>
    /// <param name="profile">The approved profile.</param>
    /// <returns>The repo entries, in profile order.</returns>
    internal static IReadOnlyList<ProfileRepoEntry> For(SindriProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        return [.. (profile.Repos ?? []).Select(r => new ProfileRepoEntry(r.Name, r.Path, RepoDenyGlobs.For(profile, r), Pins(profile, r)))];
    }

    private static IReadOnlyList<ConceptPin>? Pins(SindriProfile profile, RepoEntry repo) =>
        RepoConcepts.For(profile, repo) is { Count: > 0 } concepts
            ? [.. concepts.Select(c => new ConceptPin(c.Id is { } id ? ConceptId.From(id.Value) : null, c.Name ?? string.Empty, c.Summary, c.Features ?? [], c.Paths ?? []))]
            : null;
}
