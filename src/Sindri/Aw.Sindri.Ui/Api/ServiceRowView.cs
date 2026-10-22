namespace Aw.Sindri.Ui.Api;

/// <summary>A scheduled service in GET /sindri/services.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Every">Its cadence, as the profile writes it (5s, 30m).</param>
/// <param name="Paused">True while paused.</param>
/// <param name="LastRun">Its latest run, or null when it has not run.</param>
/// <param name="NextSlot">When it runs next, or null when none is due.</param>
/// <param name="Job">The job of a manual run in flight, or null.</param>
internal sealed record ServiceRowView(string Name, string Every, bool Paused, ServiceRunView? LastRun = null, DateTimeOffset? NextSlot = null, string? Job = null);
