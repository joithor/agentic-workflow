using Aw.Context.Core.Models;

namespace Aw.Context.Features.Adjudicate;

/// <summary>Stub.</summary>
internal sealed class Adjudicator(IModelPort model)
{
    public ValueTask<Adjudication> Judge(AdjudicationRequest request, CancellationToken cancellationToken)
    {
        _ = (model, request, cancellationToken);
        throw new NotSupportedException();
    }
}
