using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.GhPublish;

/// <summary>Stub (cutover unit 1.2 fills it): the run directory is absolute, the repo is owner/name, the PR number is 1 or more.</summary>
internal sealed class GhPublishValidator : IValidator<GhPublishRequest>
{
    public Result<Unit> Validate(GhPublishRequest value) => Unit.Value;
}
