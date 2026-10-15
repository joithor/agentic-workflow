namespace Aw.Sindri.Ui.ViewModels.Shell;

/// <summary>A view under "Later ▾" that the API doesn't serve yet (UI spec §3.2, U12): shown dimmed, never faked.</summary>
/// <param name="Label">Agents, Repos, Today or Automatic work.</param>
/// <param name="Note">"not served yet".</param>
internal sealed record LaterEntry(string Label, string Note);
