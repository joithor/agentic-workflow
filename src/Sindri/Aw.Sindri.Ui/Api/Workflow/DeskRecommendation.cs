using System.Text.Json.Serialization;

namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The outcome the step before a stop recommends.</summary>
/// <param name="Outcome">One of the desk step's outcomes.</param>
/// <param name="Why">The reason in plain words; read as optional.</param>
internal sealed record DeskRecommendation(
    string Outcome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Why = null);
