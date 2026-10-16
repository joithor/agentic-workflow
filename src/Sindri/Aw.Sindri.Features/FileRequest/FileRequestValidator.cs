using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Sindri.Features.FileRequest;

/// <summary>The shape of a request; the rules (RequestRules.Validate, SND-REQ-001) arrive with the implementation.</summary>
internal sealed class FileRequestValidator : IValidator<FileRequestRequest>
{
    public Result<Unit> Validate(FileRequestRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Unit.Value;
    }
}
