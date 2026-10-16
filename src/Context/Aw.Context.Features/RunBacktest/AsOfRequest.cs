using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;

namespace Aw.Context.Features.RunBacktest;

/// <summary>The as-of map of one sample.</summary>
/// <param name="Repo">The repo: its name and checkout.</param>
/// <param name="Sample">The sample (its cut commit and the issue's creation time decide the map).</param>
/// <param name="Scope">The call's scope: the deny globs and the caller the map is viewed under.</param>
internal sealed record AsOfRequest(ContextRepo Repo, CorpusSample Sample, ContextScope Scope);
