using System.Globalization;
using Aw.Context.Core;
using Aw.Kernel;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>A depth as a caller sent it: absent, or an integer. The range is the engine's to check (SND-CONTEXT-006).</summary>
internal static class ContextDepth
{
    internal static Result<int?> Parse(string? text) =>
        text is null ? (int?)null
        : int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var depth) ? depth
        : ContextErrors.QueryInvalid.With("depth is a whole number, 0 to 4 (fix: --depth 2)");
}
