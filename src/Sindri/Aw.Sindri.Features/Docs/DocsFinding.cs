using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Docs;

/// <summary>One finding of the docs check, in the shape the line's other Steps write ('fact:findings').</summary>
internal static class DocsFinding
{
    /// <summary>A failing finding.</summary>
    /// <param name="rule">The rule id.</param>
    /// <param name="item">The doc it is about; null when it is about the worktree.</param>
    /// <param name="message">What is wrong.</param>
    /// <param name="fix">What to do.</param>
    /// <returns>The finding.</returns>
    internal static JsonNode Of(string rule, string? item, string message, string fix) =>
        new JsonObject { ["rule"] = rule, ["item"] = item, ["message"] = message, ["fix"] = fix, ["severity"] = "fail" };
}
