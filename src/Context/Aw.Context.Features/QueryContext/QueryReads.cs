using Aw.Context.Core.Engine;
using Aw.Context.Core.Map;

namespace Aw.Context.Features.QueryContext;

/// <summary>Which map a checked question reads (QueryRules.Check has already refused a draft together with a version and a malformed one).</summary>
internal static class QueryReads
{
    internal static MapRead Of(ContextQuery query) => MapRead.Of(query.Draft, query.MapVersion) is { IsSuccess: true } read ? read.Value : MapRead.Current;
}
