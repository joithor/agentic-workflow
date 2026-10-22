using System.Text.Json.Nodes;
using Aw.Sindri.Core.LensPacks;

namespace Aw.Sindri.Features.Line;

/// <summary>The style pack an instance pinned in 'fact:packs' (line spec §5.1, §5.3): the intake writes { id, hash } of the pack it resolved, and the checks read the same pack again and refuse one that changed.</summary>
internal static class StylePin
{
    /// <summary>The { id, hash } entry the intake pins for a resolved pack.</summary>
    /// <param name="id">The pack id.</param>
    /// <param name="hash">The pack hash.</param>
    /// <returns>The entry.</returns>
    internal static JsonObject Entry(string id, string hash) => new() { ["id"] = id, ["hash"] = hash };

    /// <summary>The pinned pack, read again.</summary>
    /// <param name="styles">The style packs.</param>
    /// <param name="packs">The 'fact:packs' tree.</param>
    /// <returns>The pack, or null when nothing was pinned; or why the pin can't be honoured (SND-PACK-001 no such pack, SND-PACK-002 the pack's hash differs).</returns>
    internal static Pinned Read(IStylePacks styles, JsonNode? packs)
    {
        ArgumentNullException.ThrowIfNull(styles);
        var pin = LineJson.Obj(packs, "style");
        if (LineJson.Str(pin, "id") is not { } id)
        {
            return new Pinned(Pack: null, Failure: null);
        }

        if (!LensPackId.TryFrom(id, out var packId))
        {
            return new Pinned(Pack: null, $"SND-PACK-001: the style pack '{id}' is not a built-in pack");
        }

        var found = styles.Find(packId);
        if (found.IsError)
        {
            return new Pinned(Pack: null, $"SND-PACK-001: {found.Error.Message}");
        }

        var pinned = LineJson.Str(pin, "hash");
        return string.Equals(pinned, found.Value.Hash, StringComparison.Ordinal)
            ? new Pinned(found.Value, Failure: null)
            : new Pinned(Pack: null, $"SND-PACK-002: the style pack '{id}' is {found.Value.Hash} now and the run pinned {pinned} (fix: start the design again from intake, which pins the pack as it is)");
    }
}
