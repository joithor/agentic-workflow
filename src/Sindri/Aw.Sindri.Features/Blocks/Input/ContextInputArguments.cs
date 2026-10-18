using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;

namespace Aw.Sindri.Features.Blocks.Input;

/// <summary>Reads the 'with:' arguments the context Step inputs share.</summary>
internal static class ContextInputArguments
{
    /// <summary>The anchors: a list of strings, or one string; empty when absent or of another shape.</summary>
    /// <param name="context">The input's context.</param>
    /// <returns>The anchors, in order, duplicates kept (the engine counts them once).</returns>
    internal static IReadOnlyList<string> Anchors(StepInputContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (!context.Arguments.TryGetValue("anchors", out var value))
        {
            return [];
        }

        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString() is { Length: > 0 } one ? [one] : [],
            JsonValueKind.Array => [.. value.EnumerateArray().Where(static e => e.ValueKind == JsonValueKind.String).Select(static e => e.GetString() ?? string.Empty).Where(static s => s.Length > 0)],
            _ => [],
        };
    }
}
