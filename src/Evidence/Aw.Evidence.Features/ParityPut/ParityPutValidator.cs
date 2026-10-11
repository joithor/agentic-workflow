using Aw.Evidence.Core;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Evidence.Features.ParityPut;

internal sealed class ParityPutValidator : IValidator<ParityPutRequest>
{
    public Result<Unit> Validate(ParityPutRequest value) =>
        value.UploadFile.StartsWith('/') && value.File.StartsWith('/')
            ? Unit.Value
            : EvidenceErrors.Invalid.With("the upload and image paths must be absolute");
}
