using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core;

namespace Aw.Meta.Features.OwnerAsk;

internal sealed class AskOwnerValidator : IValidator<AskOwnerRequest>
{
    public Result<Unit> Validate(AskOwnerRequest value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return string.IsNullOrWhiteSpace(value.Text) ? MetaErrors.Invalid.With("a question needs some text") : Unit.Value;
    }
}
