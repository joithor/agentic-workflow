using Aw.Meta.Core.Owner;

namespace Aw.Meta.Tests.Messages;

public sealed class OwnerQuestionTextTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("OWNER QUESTION: dark mode first?", "dark mode first?")]
    [InlineData("  OWNER QUESTION - which db  ", "which db")]
    [InlineData("OWNER QUESTION", "")]
    [InlineData("a plain message", "a plain message")]
    public void The_lead_in_and_its_separator_are_stripped(string body, string question) =>
        Assert.Equal(question, OwnerQuestionText.From(body));
}
