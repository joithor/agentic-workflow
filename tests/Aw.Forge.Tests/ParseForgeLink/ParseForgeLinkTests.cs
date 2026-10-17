using Aw.Forge.Features.ParseForgeLink;
using Aw.Forge.Testing;

namespace Aw.Forge.Tests.ParseForgeLink;

public sealed class ParseForgeLinkTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task ParseForgeLink_parses_a_github_pull_link_into_a_forge_ref()
    {
        var service = new ParseForgeLinkService();
        var request = A.ParseForgeLinkRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert parses_a_github_pull_link_into_a_forge_ref");
    }
}
