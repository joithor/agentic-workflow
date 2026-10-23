namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The open desk task's form.</summary>
/// <param name="Run">Run, as served.</param>
/// <param name="Outputs">Outputs, as served.</param>
/// <param name="Checks">Checks, as served.</param>
/// <param name="CompleteAction">CompleteAction, as served.</param>
/// <param name="Reviews">What the person reads before answering; null or empty when none is served.</param>
/// <param name="Recommended">The outcome the step before the stop recommends; read as optional.</param>
internal sealed record DeskForm(
    string Run,
    IReadOnlyList<DeskOutput> Outputs,
    IReadOnlyList<string> Checks,
    string CompleteAction,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] DeskRecommendation? Recommended = null,
    [property: System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<DeskReview>? Reviews = null);
