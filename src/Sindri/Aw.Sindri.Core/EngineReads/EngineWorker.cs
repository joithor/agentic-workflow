namespace Aw.Sindri.Core.EngineReads;

/// <summary>The worker's facts, from the task queue's description (API spec §3.8, E-30).</summary>
/// <param name="Count">The workers that polled the task queue lately.</param>
/// <param name="Identity">The most recent poller's identity; null when none polled.</param>
/// <param name="TaskQueue">The task queue the worker polls.</param>
/// <param name="Polling">Whether a worker polled in the last minute.</param>
/// <param name="ActivitiesRunning">The activities running across the instances.</param>
/// <param name="Backlog">The tasks waiting on the queue.</param>
/// <param name="LastHeartbeatSeconds">Seconds since the last poll; null when none polled.</param>
public sealed record EngineWorker(int Count, string? Identity, string TaskQueue, bool Polling, int ActivitiesRunning, long Backlog, long? LastHeartbeatSeconds);
