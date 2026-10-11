using Aw.Evidence.Features.ParityComment;
using Aw.Evidence.Testing;
using Aw.Kernel.Json;

namespace Aw.Evidence.Tests.ParityPublishing;

public sealed class ParityCommentTests
{
    private static readonly InMemoryRunFiles _files = new InMemoryRunFiles()
        .With("/runs/parity-1/parity.json", AwJson.Serialize(A.ParitySummary().Build(), TestJson.Options));

    [Theory, Trait("Lane", "unit")]
    [InlineData(CommentTarget.Linear, "**Design parity** — build unverified")]
    [InlineData(CommentTarget.GitHub, "## Design parity (seeded data, images on Linear)")]
    public async Task ParityComment_renders_the_comment_for_its_target(CommentTarget target, string firstLine)
    {
        var result = await new ParityCommentService(_files, TestJson.Options).Handle(new("/runs/parity-1", target), CancellationToken.None);

        Assert.StartsWith(firstLine, result.Value.Markdown, StringComparison.Ordinal);
    }
}
