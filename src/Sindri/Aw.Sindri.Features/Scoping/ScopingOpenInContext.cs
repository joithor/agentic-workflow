using System.Text.Json.Serialization;

namespace Aw.Sindri.Features.Scoping;

/// <summary>U7's action: enabled once the evidence step has assembled the packet; before that it is disabled (still focusable) and says why.</summary>
/// <param name="Enabled">Whether it opens.</param>
/// <param name="Reason">Why it is disabled, or null.</param>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ScopingOpenInContext(bool Enabled, string? Reason);
