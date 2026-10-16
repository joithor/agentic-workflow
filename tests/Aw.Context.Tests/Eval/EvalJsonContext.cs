using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;

namespace Aw.Context.Tests.Eval;

/// <summary>The probe file and report types for tests (in production the Sindri host's JSON context holds them).</summary>
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
[JsonSerializable(typeof(Aw.Context.Core.Extract.ExtractedMap))]
[JsonSerializable(typeof(Aw.Context.Core.Eval.Backtest.BacktestReport))]
[JsonSerializable(typeof(Aw.Context.Core.Eval.MapQuality.MapQualityReport))]
internal sealed partial class EvalJsonContext : JsonSerializerContext;
