using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>One thing an open desk has the person review.</summary>
/// <param name="Fact">The fact key.</param>
/// <param name="Kind">design, plan, scope, packet, pr, questions or text, as served.</param>
/// <param name="Path">The file or packet directory, when the fact's value names one; read as optional.</param>
/// <param name="Url">The link, when the fact's value names one; read as optional.</param>
internal sealed record DeskReview(
    string Fact,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Url = null);
