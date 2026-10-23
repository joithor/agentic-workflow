namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Rechecks of a red check.</summary>
/// <param name="Runs">Rechecks run.</param>
/// <param name="Resolved">Rechecks that cleared it.</param>
/// <param name="Unresolved">Rechecks that did not.</param>
internal sealed record BabysitterRechecksView(int Runs, int Resolved, int Unresolved);
