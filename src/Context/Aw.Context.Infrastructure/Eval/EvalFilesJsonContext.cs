using System.Text.Json.Serialization;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Eval.Backtest;
using Aw.Context.Core.Eval.MapQuality;
using Aw.Context.Core.Extract;

namespace Aw.Context.Infrastructure.Eval;

/// <summary>
/// The types the eval file stores read and write (probe sets and reports, the backtest and map-quality reports, the as-of map cache). The stores
/// serialise through this context, not the host's, so they work whatever the host registers (and under NativeAOT, where reflection is off).
/// </summary>
[JsonSerializable(typeof(ProbeSet))]
[JsonSerializable(typeof(ProbeReport))]
[JsonSerializable(typeof(ExtractedMap))]
[JsonSerializable(typeof(BacktestReport))]
[JsonSerializable(typeof(MapQualityReport))]
internal sealed partial class EvalFilesJsonContext : JsonSerializerContext;
