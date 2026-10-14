namespace Aw.Context.Core.Trust;

/// <summary>
/// Scrubs one output field before it is serialized (spec §7 egress): CLI, HTTP and MCP output all reach a model. A
/// field the scrubber blocks becomes '[REDACTED:blocked]' in that field only, so the response stays valid JSON; it
/// never falls back to the unscrubbed value.
/// </summary>
public interface IEgressScrubber
{
    /// <summary>Scrubs <paramref name="value"/>.</summary>
    /// <param name="value">One field's text.</param>
    /// <returns>The scrubbed field.</returns>
    public ScrubbedField Scrub(string value);
}
