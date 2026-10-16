using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;

namespace Aw.Context.Tests.Eval;

/// <summary>The probe file and report types for tests (in production the Sindri host's JSON context holds them).</summary>
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
[JsonSerializable(typeof(Context.Core.Extract.ExtractedMap))]
[JsonSerializable(typeof(Context.Core.Eval.Backtest.BacktestReport))]
[JsonSerializable(typeof(Context.Core.Eval.MapQuality.MapQualityReport))]
internal sealed partial class EvalJsonContext : JsonSerializerContext;
