using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>The one mapping from the context engine's view JSON to the scoping facts (scoping spec §5.2, C1, C3, C5), shared by the desk and the block runtime.</summary>
internal static class LensGather
{
    /// <summary>Maps the views into the lenses fact and the coverage fact.</summary>
    internal static LensGatherResult Assemble(JsonNode? lenses, JsonNode? code, JsonNode? history, IReadOnlyList<string> anchors) =>
        throw new NotSupportedException();
}
