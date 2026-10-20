namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>A previously approved recipe the plan leaves out because it gained a capability.</summary>
/// <param name="Id">The recipe id.</param>
/// <param name="DisplayName">The name the app shows; optional.</param>
/// <param name="Reason">'gains-capability' or 'waits-for-child'.</param>
/// <param name="Sentence">Why, in a sentence, served.</param>
/// <param name="Added">What it newly may do.</param>
internal sealed record OwnApprovalRecipe(string Id, string? DisplayName, string Reason, string Sentence, IReadOnlyList<CapabilityLine> Added);
