using Aw.Context.Core.Eval.MapQuality;
using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Measures the current map against the owner's decisions (GET /sindri/context/eval/map): node and link precision and the unmapped rate. A query, so no catalog row.</summary>
public sealed record GetContextEvalMapRequest : IQuery<MapQualityRun>;
