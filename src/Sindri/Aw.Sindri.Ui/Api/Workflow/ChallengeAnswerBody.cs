using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A challenge's answer: its id and the text typed, or how long the button was held.</summary>
/// <param name="Challenge">The challenge id.</param>
/// <param name="Typed">The typed text; empty for a hold.</param>
/// <param name="HeldMs">Milliseconds held; left out when typed.</param>
internal sealed record ChallengeAnswerBody(
    string Challenge,
    string Typed = "",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] long? HeldMs = null);
