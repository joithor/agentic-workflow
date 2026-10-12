using Aw.Context.Core.Onyx;
using Aw.Context.Core.Sources;

namespace Aw.Context.Infrastructure.Onyx;

/// <summary>
/// Doctor's 'context:onyx' version check (spec §8.8): 'GET &lt;url&gt;/api/version' with no Authorization header (the
/// route is public in Onyx), and the reported 'backend_version' compared with the pinned release.
/// </summary>
internal sealed class OnyxVersionProbe
{
    public OnyxVersionProbe(HttpClient http)
    {
        ArgumentNullException.ThrowIfNull(http);
    }

    /// <summary>Asks Onyx for its version.</summary>
    /// <param name="settings">The checked settings.</param>
    /// <param name="deadline">How long Onyx gets to answer.</param>
    /// <param name="cancellationToken">Cancels the probe.</param>
    /// <returns>Ok, or Unavailable with SND-CONTEXT-010 (no answer) or SND-CONTEXT-055 (another version).</returns>
    public ValueTask<SourceHealth> Check(OnyxSettings settings, TimeSpan deadline, CancellationToken cancellationToken) =>
        throw new NotSupportedException("red: OnyxVersionProbe.Check");
}
