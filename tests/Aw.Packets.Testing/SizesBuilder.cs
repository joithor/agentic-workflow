using Aw.Kernel.Json;
using Aw.Packets.Core.Model;

namespace Aw.Packets.Testing;

/// <summary>Builds sizes files: the default limits, and their bytes.</summary>
internal static class SizesBuilder
{
    /// <summary>The default sizes file (A.Sizes()).</summary>
    /// <returns>The sizes.</returns>
    public static SizesFile Default() => new()
    {
        SchemaVersion = 1,
        Sizes = new SizeLimits
        {
            ExtraSmall = Limit(3, 2, 50_000, 20, 30),
            Small = Limit(8, 4, 150_000, 60, 90),
            Medium = Limit(20, 8, 400_000, 120, 180),
            Large = Limit(40, 16, 900_000, 200, 300),
            ExtraLarge = Limit(80, 32, 2_000_000, 400, 600),
        },
    };

    /// <summary>A limit.</summary>
    /// <param name="files">Most files.</param>
    /// <param name="items">Most items.</param>
    /// <param name="tokens">Most tokens.</param>
    /// <param name="turns">Most turns.</param>
    /// <param name="wallMinutes">Most wall minutes.</param>
    /// <returns>The limit.</returns>
    public static SizeLimit Limit(int files, int items, long tokens, int turns, int wallMinutes) =>
        new() { Files = files, Items = items, Budget = new TaskBudget { Tokens = tokens, Turns = turns, WallMinutes = wallMinutes } };

    /// <summary>The file's bytes, as Sindri would write them.</summary>
    /// <param name="sizes">The sizes.</param>
    /// <returns>Indented JSON.</returns>
    public static byte[] Bytes(SizesFile sizes) => AwJson.Serialize(sizes, PacketFiles.Json);
}
