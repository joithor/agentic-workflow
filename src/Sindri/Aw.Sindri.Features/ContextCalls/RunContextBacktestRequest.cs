using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Runs the map backtest now (context spec §9.1, API spec §4.1): a job-class command that starts a job and answers its links. It replays the merged work
/// of the last months against maps rebuilt as of each cut, scores the arms and writes the full report under the eval file store. It takes a heavy-job
/// slot and runs for minutes. A terminal, the app and the engine's own services may start it; a session may not (the gate refuses it before the job exists).
/// </summary>
/// <param name="Arms">The arms, comma-separated ('brief,graph'), or 'all'/null for every available arm; an unknown arm is refused at the start (SND-CONTEXT-082).</param>
/// <param name="Since">Only issues created at or after this, or null.</param>
/// <param name="Until">Only issues created at or before this instant, or null. It is an exact instant here; the CLI turns a date alone ('2026-09-30') into that day's last tick before it posts.</param>
/// <param name="Limit">At most this many samples (the earliest first), or null.</param>
public sealed record RunContextBacktestRequest(string? Arms = null, DateTimeOffset? Since = null, DateTimeOffset? Until = null, int? Limit = null) : IJob<RunContextBacktestResult>;
