namespace Aw.Sindri.Ui.Api;

/// <summary>A scheduled service in the status.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence, as the profile writes it (5s, 30m).</param>
/// <param name="LastRun">When it last ran, if ever.</param>
internal sealed record ServiceView(string Name, string Every, DateTimeOffset? LastRun);
