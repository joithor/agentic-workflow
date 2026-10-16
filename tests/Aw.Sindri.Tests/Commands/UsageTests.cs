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

    /// <summary>Output.Error exits 1 only for 'not running' and 2 for everything else, malformed input included (review run 1).</summary>
    [Theory, Trait("Lane", "unit")]
    [InlineData("start")]
    [InlineData("status")]
    [InlineData("list")]
    [InlineData("events")]
    [InlineData("answer")]
    [InlineData("command")]
    [InlineData("cancel")]
    [InlineData("complete")]
    public void Help_gives_every_workflow_verb_the_exit_codes_the_output_uses(string verb) =>
        Assert.Contains("1 not running, 2 invalid, refused or error", Assert.Single(Lines(), l => l.StartsWith($"sindri workflow {verb} ", StringComparison.Ordinal)), StringComparison.Ordinal);

    [Theory, Trait("Lane", "unit")]
    [InlineData("start", "[--json]")]
    [InlineData("answer", "[--nonce <n>] [--json]")]
    [InlineData("command", "[--nonce <n>] [--json]")]
    [InlineData("cancel", "[--nonce <n>] [--json]")]
    public void The_workflow_error_usage_names_the_flags_each_verb_takes(string verb, string flags)
    {
        var usage = WorkflowCommandTable.UsageText;
        var verbs = new[] { "start", "status", "list", "events", "answer", "command", "cancel", "complete" };
        var from = usage.IndexOf(verb == "start" ? "workflow start " : $" | {verb} ", StringComparison.Ordinal);
        var end = verbs.Select(v => usage.IndexOf($" | {v} ", from + 1, StringComparison.Ordinal)).Where(i => i > 0).DefaultIfEmpty(usage.Length).Min();
        var part = usage[from..end];

        Assert.Contains(flags, part, StringComparison.Ordinal);
    }

    [Theory, Trait("Lane", "unit")]
    [InlineData("onyx status")]
    [InlineData("onyx up")]
    [InlineData("onyx down")]
    public void Usage_lists_the_onyx_verbs(string verb) =>
        Assert.Single(Lines(), l => l.StartsWith($"sindri {verb} ", StringComparison.Ordinal) || l.StartsWith($"sindri {verb}\t", StringComparison.Ordinal));

    private static IEnumerable<string> Lines() => Usage.Text.Split('\n').Select(static l => l.Trim().Replace("usage: ", string.Empty, StringComparison.Ordinal));
}
