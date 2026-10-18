namespace Aw.Sindri.Ui.Api.Context;

/// <summary>Intent counts of a file map entry.</summary>
/// <param name="Records">Records, as served.</param>
/// <param name="Decisions">Decisions, as served.</param>
internal sealed record FileIntent(
    int Records,
    int Decisions);
