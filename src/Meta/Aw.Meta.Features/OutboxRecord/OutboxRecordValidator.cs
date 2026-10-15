using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Meta.Features.OutboxRecord;

internal sealed class OutboxRecordValidator : IValidator<OutboxRecordRequest>
{
    public Result<Unit> Validate(OutboxRecordRequest value) => throw new NotSupportedException();
}
