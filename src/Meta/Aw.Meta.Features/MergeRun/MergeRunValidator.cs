using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.MergeRun;

internal sealed class MergeRunValidator : IValidator<MergeRunRequest>
{
    public Result<Unit> Validate(MergeRunRequest value) => throw new NotSupportedException();
}
