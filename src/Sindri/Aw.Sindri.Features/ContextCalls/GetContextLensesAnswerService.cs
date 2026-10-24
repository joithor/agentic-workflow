using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Picks the single or the set read by the number of distinct anchors (none or one: schema 1), and runs it.</summary>
internal sealed class GetContextLensesAnswerService(IService<GetContextLensesRequest, LensesView> single, IService<GetContextLensesSetRequest, LensesSetView> set)
    : IService<GetContextLensesAnswerRequest, LensesAnswer>
{
    // why: one paging rule for every tool and transport: a call that names no limit gets this many items from each list, over HTTP as over MCP
    private const int DefaultLimit = 100;

    public async ValueTask<Result<LensesAnswer>> Handle(GetContextLensesAnswerRequest request, CancellationToken cancellationToken)
    {
        if (QueryRules.CheckPage(request.Limit, request.Offset) is { } refused)
        {
            return refused;
        }

        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToArray();
        if (anchors.Length > 1)
        {
            var many = await set.Handle(new GetContextLensesSetRequest(anchors, request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
            return many.IsError ? many.Error : new LensesAnswer(One: null, Many: Page(many.Value, request));
        }

        var one = await single.Handle(new GetContextLensesRequest(anchors.Length == 0 ? string.Empty : anchors[0], request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
        return one.IsError ? one.Error : new LensesAnswer(Page(one.Value, request), Many: null);
    }

    private static LensesView Page(LensesView view, GetContextLensesAnswerRequest request) =>
        LensPages.Window(view, request.Limit ?? DefaultLimit, request.Offset ?? 0);

    private static LensesSetView Page(LensesSetView view, GetContextLensesAnswerRequest request) =>
        LensPages.Window(view, request.Limit ?? DefaultLimit, request.Offset ?? 0);
}
