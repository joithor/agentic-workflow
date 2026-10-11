using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityComment;

internal sealed class ParityCommentValidator : IValidator<ParityCommentRequest>
{
    public Result<Unit> Validate(ParityCommentRequest value) =>
        value.RunDirectory.StartsWith('/') ? Unit.Value : EvidenceErrors.Invalid.With("the run directory must be absolute");
}
