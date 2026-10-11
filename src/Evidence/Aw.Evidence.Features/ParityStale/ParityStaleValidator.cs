using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityStale;

internal sealed class ParityStaleValidator : IValidator<ParityStaleRequest>
{
    public Result<Unit> Validate(ParityStaleRequest value) =>
        value.AttachmentsFile.StartsWith('/') ? Unit.Value : EvidenceErrors.Invalid.With("the attachments file path must be absolute");
}
