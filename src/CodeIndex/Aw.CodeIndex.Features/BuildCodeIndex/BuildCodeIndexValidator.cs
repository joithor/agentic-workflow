using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.CodeIndex.Features.BuildCodeIndex;

internal sealed class BuildCodeIndexValidator : IValidator<BuildCodeIndexRequest>
{
    public Result<Unit> Validate(BuildCodeIndexRequest value) => Unit.Value;
}
