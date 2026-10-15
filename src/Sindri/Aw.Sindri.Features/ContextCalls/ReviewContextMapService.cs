using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// One review action (context spec §8.7, README X63). The desk variants act at once: one override row each (the bulk accept: one per
/// deterministic entry), recorded as 'context.map.reviewed'. The protected variants (apply-file, merge, add, delete) first show what
/// they would do and issue the challenge 'review &lt;verb&gt; &lt;hash6&gt;', bound to the request's fields (and, for a file, its content);
/// with an answer that redeems it, they act. The chain has already refused a caller the verb's class doesn't allow.
/// </summary>
internal sealed class ReviewContextMapService : IService<ReviewContextMapRequest, ReviewContextMapResponse>
{
    public ValueTask<Result<ReviewContextMapResponse>> Handle(ReviewContextMapRequest request, CancellationToken cancellationToken) => throw new NotSupportedException(ReviewInput.Read(request).IsError.ToString());
}
