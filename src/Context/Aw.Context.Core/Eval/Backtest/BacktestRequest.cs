using Aw.Context.Core.Engine;

namespace Aw.Context.Core.Eval.Backtest;

/// <summary>Runs the backtest (spec §9.1): a human or service job.</summary>
/// <param name="Scope">The caller and the approved 'context:' section.</param>
/// <param name="Arms">The arms to run, in report order (<see cref="Arms.Parse"/>); an arm that is not available yet prints as such.</param>
/// <param name="Since">Only samples whose issue was created at or after this, or null.</param>
/// <param name="Until">Only samples whose issue was created at or before this, or null.</param>
/// <param name="Limit">At most this many samples (the earliest first), or null.</param>
/// <param name="Progress">Receives the progress lines ('corpus: 5 samples', 'sample 3/5 SPL-103: map cached', 'arm graph 3/5').</param>
public sealed record BacktestRequest(ContextScope Scope, IReadOnlyList<Arm> Arms, DateTimeOffset? Since = null, DateTimeOffset? Until = null, int? Limit = null, IProgress<string>? Progress = null);
