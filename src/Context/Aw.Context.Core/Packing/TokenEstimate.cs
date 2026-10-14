using System.Text;

namespace Aw.Context.Core.Packing;

/// <summary>Tokens estimated as UTF-8 bytes / 4, rounded up (spec §8.3); results label it 'estimate'.</summary>
public static class TokenEstimate
{
    /// <summary>The estimate for <paramref name="text"/>.</summary>
    /// <param name="text">The text, or null (0).</param>
    /// <returns>The tokens.</returns>
    public static int Of(string? text) => text is null ? 0 : (Encoding.UTF8.GetByteCount(text) + 3) / 4;
}
