using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.LogAppend;

internal sealed class AppendLogValidator : IValidator<AppendLogRequest>
{
    public Result<Unit> Validate(AppendLogRequest value) => throw new NotSupportedException();
}
