using Aw.Context.Core.Engine;

namespace Aw.Context.Features.QueryContext;

/// <summary>How far and through which hops a query's cascade reaches: its depth and its options (the query was checked, so its hops parse).</summary>
/// <param name="Depth">The cascade depth.</param>
/// <param name="Options">The hops and the component fan-out cap.</param>
internal readonly record struct CascadeReach(int Depth, CascadeOptions Options)
{
    internal static CascadeReach Of(ContextQuery query, ContextSettings settings) =>
        new(query.Depth ?? settings.CascadeDepth, QueryRules.CascadeFor(QueryRules.ParseHops(query.Hops).Value, settings));
}
