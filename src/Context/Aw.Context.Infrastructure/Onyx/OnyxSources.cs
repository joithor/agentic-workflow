using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Builds an onyx source for the profile's onyx settings and Linear teams (registered by AddContextInfrastructure).
/// Build one per query: a source remembers what its searches said about each hit until its fetches need it.
/// </summary>
/// <param name="parts">What every onyx source shares.</param>
internal sealed class OnyxSources(OnyxSourceParts parts)
{
    /// <summary>An onyx source.</summary>
    /// <param name="settings">The checked 'context.sources.onyx' settings.</param>
    /// <param name="teams">The Linear team keys an identifier alias may name ('context.sources.linear.teams').</param>
    /// <returns>The source.</returns>
    public IContextSource Create(OnyxSettings settings, IReadOnlySet<string> teams) =>
        new OnyxSource(new OnyxClient(settings, parts), teams, parts.Clock, parts.CallDeadline);
}
