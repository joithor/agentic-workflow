using Aw.Context.Core.Engine;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Map;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.RunBacktest;

/// <summary>What one sample gives its arms.</summary>
/// <param name="Scope">The caller's scope (the pipeline gates and scrubs with it).</param>
/// <param name="Sample">The sample.</param>
/// <param name="BriefText">The brief, cleaned at ingest and cut to the question limit.</param>
/// <param name="AsOfMap">The sample's as-of map.</param>
/// <param name="Docs">The repo docs at the cut.</param>
internal sealed record ArmInputs(ContextScope Scope, CorpusSample Sample, string BriefText, VisibleMap AsOfMap, IContextSource Docs);
