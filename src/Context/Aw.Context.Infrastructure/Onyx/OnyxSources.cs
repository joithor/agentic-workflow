using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>Builds an onyx source for the profile's settings and Linear teams.</summary>
/// <param name="parts">What every onyx source shares.</param>
internal sealed class OnyxSources(OnyxSourceParts parts)
{
    /// <summary>An onyx source.</summary>
    /// <param name="settings">The checked 'context.sources.onyx' settings.</param>
    /// <param name="teams">The Linear team keys an identifier alias may name.</param>
    /// <returns>The source.</returns>
    public IContextSource Create(OnyxSettings settings, IReadOnlySet<string> teams)
    {
        _ = (parts, settings, teams, OnyxRequests.Allowlist);
        throw new NotSupportedException("red: the onyx source");
    }
}
