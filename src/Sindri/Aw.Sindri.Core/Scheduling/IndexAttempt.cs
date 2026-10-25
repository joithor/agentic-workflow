namespace Aw.Sindri.Core.Scheduling;

/// <summary>A finished code index build job, as the outlook rules read it (<see cref="SyncAttempt"/> stays the shared one of the other services).</summary>
/// <param name="Failed">True when the job failed (a succeeded or cancelled one resets the count).</param>
/// <param name="EndedAt">When it ended.</param>
public sealed record IndexAttempt(bool Failed, DateTimeOffset EndedAt);
