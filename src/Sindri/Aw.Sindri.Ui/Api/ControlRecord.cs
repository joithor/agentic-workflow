namespace Aw.Sindri.Ui.Api;

/// <summary>The control plane's endpoint record (control.json): where it listens, its pid and its lease epoch.</summary>
/// <param name="Url">The API's base URL.</param>
/// <param name="Pid">The control plane's process id.</param>
/// <param name="Epoch">Its lease epoch; checked against /sindri/ping, so a stale record reads as not running.</param>
internal sealed record ControlRecord(Uri Url, int Pid, long Epoch);
