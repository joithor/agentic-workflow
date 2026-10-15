namespace Aw.Sindri.Ui.ViewModels;

/// <summary>A scheduled service as the window shows it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Cadence">"every 5s".</param>
/// <param name="LastRun">"last run 12:01:30Z" or "not run yet".</param>
internal sealed record ServiceRow(string Name, string Cadence, string LastRun);
