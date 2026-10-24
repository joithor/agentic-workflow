using Aw.Context.Core.Engine;
using Aw.Context.Core.Lenses;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>Picks the single or the set read by the number of distinct anchors (none or one: schema 1), and runs it.</summary>
internal sealed class GetContextLensesAnswerService(IService<GetContextLensesRequest, LensesView> single, IService<GetContextLensesSetRequest, LensesSetView> set)
    : IService<GetContextLensesAnswerRequest, LensesAnswer>
{
    public async ValueTask<Result<LensesAnswer>> Handle(GetContextLensesAnswerRequest request, CancellationToken cancellationToken)
    {
        var anchors = request.Anchors.Distinct(StringComparer.Ordinal).ToArray();
        if (anchors.Length > 1)
        {
            var many = await set.Handle(new GetContextLensesSetRequest(anchors, request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
            return many.IsError ? many.Error : new LensesAnswer(One: null, Many: many.Value);
        }

        if (anchors.Length == 0)
        {
            return QueryRules.CheckAnchors(anchors, "lenses")!;
        }

        var one = await single.Handle(new GetContextLensesRequest(anchors[0], request.Draft, request.Depth, request.MapVersion, request.Hops), cancellationToken).ConfigureAwait(false);
        return one.IsError ? one.Error : new LensesAnswer(One: one.Value, Many: null);
    }
}
