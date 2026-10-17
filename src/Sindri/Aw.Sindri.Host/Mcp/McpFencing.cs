using System.Text.Json;
using Aw.Context.Core.Trust;

namespace Aw.Sindri.Host.Mcp;

/// <summary>
/// Fences the untrusted free text of a sindri_* result (API spec §8, "the same trust field and fencing as context_* results"):
/// each planned string is wrapped by <see cref="UntrustedFence"/> and its object gains a trust member set to "untrusted". Nothing
/// else changes, so structuredContent equals the HTTP body apart from those values and members.
/// </summary>
internal static class McpFencing
{
    /// <summary>The member that marks an object as holding fenced text.</summary>
    internal const string TrustMember = "trust";

    /// <summary>The value of <see cref="TrustMember"/>.</summary>
    internal const string Untrusted = "untrusted";

    /// <summary>The body with every field of the plan fenced.</summary>
    /// <param name="body">The route's JSON body.</param>
    /// <param name="plan">What to fence.</param>
    /// <returns>The fenced body.</returns>
    internal static JsonElement Apply(JsonElement body, FencePlan plan) => throw new NotSupportedException();
}
