namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>The profile's 'workflow.pr' keys the watcher reads.</summary>
/// <param name="Interval">'pollInterval': how often it polls.</param>
/// <param name="RemindAfter">'remindAfter': how long a pull request is quiet before its 'idle'.</param>
internal sealed record WatchSettings(TimeSpan Interval, TimeSpan RemindAfter);
