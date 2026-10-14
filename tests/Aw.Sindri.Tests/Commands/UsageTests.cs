using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.Commands;

/// <summary>'sindri help' (engine spec rev 8 §6.3): every 'workflow' verb the command table routes has its usage line.</summary>
public sealed class UsageTests
{
    [Theory, Trait("Lane", "unit")]
    [InlineData("start")]
    [InlineData("status")]
    [InlineData("list")]
    [InlineData("events")]
    [InlineData("answer")]
    [InlineData("command")]
    [InlineData("cancel")]
    [InlineData("complete")]
    public void Help_lists_every_workflow_verb(string verb) =>
        Assert.Single(Lines(), l => l.StartsWith($"sindri workflow {verb} ", StringComparison.Ordinal));

    [Fact, Trait("Lane", "unit")]
    public void Help_names_no_option_the_workflow_verbs_dropped() =>
        Assert.DoesNotContain(Lines(), l => l.StartsWith("sindri workflow ", StringComparison.Ordinal) && (l.Contains("--temporal", StringComparison.Ordinal) || l.Contains("--no-wait", StringComparison.Ordinal)));

    private static IEnumerable<string> Lines() => Usage.Text.Split('\n').Select(static l => l.Trim().Replace("usage: ", string.Empty, StringComparison.Ordinal));
}
