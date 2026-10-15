using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.OutboxRecord;

internal sealed class OutboxRecordValidator : IValidator<OutboxRecordRequest>
{
    public Result<Unit> Validate(OutboxRecordRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.IsNullOrWhiteSpace(value.To) || string.IsNullOrWhiteSpace(value.Message)
            ? MetaErrors.Invalid.With("a send needs a recipient and a message")
            : Unit.Value;
    }
}
