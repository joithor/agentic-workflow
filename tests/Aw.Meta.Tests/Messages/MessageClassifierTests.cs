using Aw.Meta.Core.Messages;

namespace Aw.Meta.Tests.Messages;

public sealed class MessageClassifierTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("OWNER QUESTION q: which db?", MessageKind.OwnerQuestion, null, null)]
    [InlineData("  OWNER QUESTION which db?", MessageKind.OwnerQuestion, null, null)]
    [InlineData("ready #190 079c2541", MessageKind.Ready, 190, null)]
    [InlineData("ready #269 079c2541\n\n- details follow in the PR body", MessageKind.Ready, 269, null)]
    [InlineData("  ready #12 0123456789abcdef0123456789abcdef01234567", MessageKind.Ready, 12, null)]
    [InlineData("ready #190", MessageKind.Request, null, null)]
    [InlineData("ready #190 abc12", MessageKind.Request, null, null)]
    [InlineData("api lane ready #190 for merge", MessageKind.LaneAnnounce, null, "api")]
    [InlineData("PR is ready #12", MessageKind.Request, null, null)]
    [InlineData("gate passes, then ready #250 079c2541", MessageKind.Request, null, null)]
    [InlineData("ready #250 follows", MessageKind.Request, null, null)]
    [InlineData("ready #264 follows once the narrow re-review is clean", MessageKind.Request, null, null)]
    [InlineData("ready #264 079c2541 not ready until CI is green", MessageKind.Request, null, null)]
    [InlineData("ready #264 079c2541 the rest follows", MessageKind.Request, null, null)]
    [InlineData("ready #269 079c2541\nChanges as follows:\n- a\n- b", MessageKind.Ready, 269, null)]
    [InlineData("ready #269 079c2541\n#268 is not ready yet", MessageKind.Ready, 269, null)]
    [InlineData("ready #269 079c2541\n\nNot ready: nothing. Follows the plan in the PR body.", MessageKind.Ready, 269, null)]
    [InlineData("Ready #269 079c2541", MessageKind.Ready, 269, null)]
    [InlineData("ready #269 `079c2541`", MessageKind.Ready, 269, null)]
    [InlineData("ready #269 079c2541xyz", MessageKind.Request, null, null)]
    [InlineData("ready #269 0123456789012345678901234567890123456789a", MessageKind.Request, null, null)]
    [InlineData("API lane here, session started", MessageKind.LaneAnnounce, null, "api")]
    [InlineData("  meta-harness lane online", MessageKind.LaneAnnounce, null, "meta-harness")]
    [InlineData("please rebase on main", MessageKind.Request, null, null)]
    [InlineData("the lane is blocked", MessageKind.Request, null, null)]
    [InlineData("", MessageKind.Request, null, null)]
    public void Bodies_are_classified(string body, MessageKind kind, int? pr, string? lane) =>
        Assert.Equal((kind, pr, lane), MessageClassifier.Classify(body) is var c ? (c.Kind, c.Pr, c.Lane) : default);
}
