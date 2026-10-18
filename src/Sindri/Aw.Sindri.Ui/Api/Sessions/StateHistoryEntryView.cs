namespace Aw.Sindri.Ui.Api.Sessions;

/// <summary>One state a session was in.</summary>
/// <param name="State">State, as served.</param>
/// <param name="Reason">Reason, as served.</param>
/// <param name="At">At, as served.</param>
/// <param name="Source">Source, as served.</param>
internal sealed record StateHistoryEntryView(string State, string? Reason, DateTimeOffset At, string Source);
