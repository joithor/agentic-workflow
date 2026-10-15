using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.SyncInbox;

internal sealed class SyncInboxValidator : IValidator<SyncInboxRequest>
{
    public Result<Unit> Validate(SyncInboxRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.IsNullOrWhiteSpace(value.Transcript)
            ? MetaErrors.Invalid.With("a transcript path is required")
            : Unit.Value;
    }
}
