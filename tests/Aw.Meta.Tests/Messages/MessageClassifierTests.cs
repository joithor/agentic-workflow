using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class MessageClassifierTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("OWNER QUESTION q: which db?", MessageKind.OwnerQuestion, null, null)]
    [InlineData("  OWNER QUESTION which db?", MessageKind.OwnerQuestion, null, null)]
    [InlineData("api lane ready #190 for merge", MessageKind.Ready, 190, null)]
    [InlineData("PR is ready #12", MessageKind.Ready, 12, null)]
    [InlineData("API lane here, session started", MessageKind.LaneAnnounce, null, "api")]
    [InlineData("  meta-harness lane online", MessageKind.LaneAnnounce, null, "meta-harness")]
    [InlineData("please rebase on main", MessageKind.Request, null, null)]
    [InlineData("the lane is blocked", MessageKind.Request, null, null)]
    [InlineData("", MessageKind.Request, null, null)]
    public void Bodies_are_classified(string body, MessageKind kind, int? pr, string? lane) =>
        Assert.Equal(new Classification(kind, pr, lane), MessageClassifier.Classify(body));
}
