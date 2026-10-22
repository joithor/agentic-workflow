using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;
using Aw.Kernel;

namespace Aw.Context.Features.QueryContext;

/// <summary>A question that passed its rules, with the map read it asked for (parsed once).</summary>
/// <param name="Valid">The checked question.</param>
/// <param name="Read">The map read.</param>
internal sealed record CheckedQuery(ValidQuery Valid, MapRead Read)
{
    // The question's own rules, then its map read (a draft together with a version, or a malformed version, is refused here, not in Core's rules).
    internal static Result<CheckedQuery> Of(ContextQuery query, ContextSettings settings)
    {
        var valid = QueryRules.Check(query, settings);
        if (valid.IsError)
        {
            return valid.Error;
        }

        var read = MapRead.Of(query.Draft, query.MapVersion);
        return read.IsError ? read.Error : new CheckedQuery(valid.Value, read.Value);
    }
}
