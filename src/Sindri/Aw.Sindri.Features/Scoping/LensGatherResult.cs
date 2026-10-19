using System.Text.Json.Nodes;

namespace Aw.Sindri.Features.Scoping;

/// <summary>What the gather mapping produced: the lenses fact and the coverage fact.</summary>
/// <param name="Lenses">One entry per lens.</param>
/// <param name="Coverage">One row per lens.</param>
internal sealed record LensGatherResult(JsonObject Lenses, JsonObject Coverage);
