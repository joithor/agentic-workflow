using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>POST /sindri/workflow/recipes/approve-all's body: the challenge and one answer, how long the button was held or the text typed.</summary>
/// <param name="Challenge">The challenge id.</param>
/// <param name="HeldMs">Milliseconds held; null when typed.</param>
/// <param name="Typed">The typed text; null when held.</param>
internal sealed record ApproveAllRequest(
    string Challenge,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? HeldMs = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Typed = null);
