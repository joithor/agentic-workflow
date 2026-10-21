using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>What a decision wrote.</summary>
/// <param name="Written">Written, as served.</param>
/// <param name="Skipped">Skipped, as served.</param>
/// <param name="Undecided">Undecided, as served.</param>
internal sealed record ReviewResult(
    IReadOnlyList<string> Written,
    IReadOnlyList<JsonElement> Skipped,
    int Undecided);
