using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Forge.Features.ParseForgeLink;

internal sealed class ParseForgeLinkService : IService<ParseForgeLinkRequest, ParseForgeLinkResponse>
{
    public ValueTask<Result<ParseForgeLinkResponse>> Handle(ParseForgeLinkRequest request, CancellationToken cancellationToken)
        => throw new NotSupportedException("ParseForgeLink: make ParseForgeLinkTests pass");
}
