using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>Stub (cutover unit 1.2 fills it): reads the run, refuses unless seeded and reviewed, commits the images through gh and writes pr-comment.md.</summary>
internal sealed class GhPublishService : IService<GhPublishRequest, GhPublishResponse>
{
    public ValueTask<Result<GhPublishResponse>> Handle(GhPublishRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("gh-publish is not built yet (cutover unit 1.2)");
}
