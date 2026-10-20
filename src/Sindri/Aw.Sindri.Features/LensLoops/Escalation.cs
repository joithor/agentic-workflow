using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.LensLoops;

/// <summary>
/// The one way a lens loop reaches a human (owner, 2026-10-19): a stopping finding carrying a typed
/// <c language="text">escalate: { kind: "ambiguousFix" | "productQuestion", question, options? }</c> (schemas/line/escalation.json). Anything else stays in the loop.
/// </summary>
internal static class Escalation
{
    /// <summary>The first typed escalation among the findings, as 'fact:escalation'.</summary>
    /// <param name="stopping">The round's findings that stop the loop, each with its lens.</param>
    /// <returns>The escalation; null when no finding carries a typed one.</returns>
    internal static JsonObject? First(IEnumerable<JsonObject> stopping)
    {
        ArgumentNullException.ThrowIfNull(stopping);
        throw new NotSupportedException();
    }
}
