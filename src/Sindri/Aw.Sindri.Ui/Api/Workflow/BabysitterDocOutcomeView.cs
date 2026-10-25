namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Docs updates that ended one way.</summary>
/// <param name="Reason">The outcome, as served.</param>
/// <param name="N">How many.</param>
internal sealed record BabysitterDocOutcomeView(string Reason, int N);
