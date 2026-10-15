using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.CheckMerge;

internal sealed class CheckMergeValidator : IValidator<CheckMergeRequest>
{
    public Result<Unit> Validate(CheckMergeRequest value) => throw new NotSupportedException();
}
