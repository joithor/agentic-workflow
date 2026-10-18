using System.Text.Json;

namespace Aw.Sindri.Ui.Api.Context;

/// <summary>POST /sindri/context/map/promote without an answer: the challenge. The app never sends it.</summary>
/// <param name="Draft">Draft, as served.</param>
/// <param name="Promoted">Promoted, as served.</param>
/// <param name="Sequence">Sequence, as served.</param>
/// <param name="Challenge">Challenge, as served.</param>
internal sealed record PromotePreview(
    string Draft,
    JsonElement? Promoted,
    long? Sequence,
    ReviewChallenge? Challenge);
