using Aw.CodeIndex.Features.BuildCodeIndex;
using Aw.CodeIndex.Testing;

namespace Aw.CodeIndex.Tests.BuildCodeIndex;

public sealed class BuildCodeIndexTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task BuildCodeIndex_build_publishes_an_index_for_a_commit()
    {
        var service = new BuildCodeIndexService();
        var request = A.BuildCodeIndexRequest().Build();

        var result = await service.Handle(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Fail("red: assert build_publishes_an_index_for_a_commit");
    }
}
