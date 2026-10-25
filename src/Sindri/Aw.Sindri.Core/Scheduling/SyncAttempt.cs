namespace Aw.Sindri.Core.Scheduling;

/// <summary>A finished sync job, as the decision reads it.</summary>
/// <param name="Failed">True when the job failed (a succeeded or cancelled one resets the count).</param>
/// <param name="EndedAt">When it ended.</param>
public sealed record SyncAttempt(bool Failed, DateTimeOffset EndedAt);
