using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Forge.Features.ParseForgeLink;

internal sealed class ParseForgeLinkValidator : IValidator<ParseForgeLinkRequest>
{
    public Result<Unit> Validate(ParseForgeLinkRequest value) => Unit.Value;
}
