using System.Text.Json;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>
/// The §9.2 eval line shapes: line 1 '{ start: { inputs }, scaffold?: true }', then '{ trigger, gates?, expect }' lines; a
/// 'child.parked' trigger carries the child's reason in 'park', an engine trigger its 'variant' and a grant its 'amount'.
/// </summary>
internal static class EvalFormat
{
    private static readonly string[] _verdicts = ["pass", "fail", "uncertain"];

    /// <summary>Asserts <paramref name="line"/> is a start line.</summary>
    /// <param name="line">The eval file's first line.</param>
    public static void AssertStart(string line)
    {
        using var json = JsonDocument.Parse(line);
        Assert.Subset(Set("start", "scaffold"), Names(json.RootElement));
        if (json.RootElement.TryGetProperty("scaffold", out var scaffold))
        {
            Assert.Equal(JsonValueKind.True, scaffold.ValueKind);
        }

        var start = json.RootElement.GetProperty("start");
        var inputs = Assert.Single(start.EnumerateObject());
        Assert.Equal("inputs", inputs.Name);
        Assert.Equal(JsonValueKind.Object, inputs.Value.ValueKind);
    }

    /// <summary>Asserts <paramref name="line"/> is a trigger line with its expectation.</summary>
    /// <param name="line">A later line of an eval file.</param>
    public static void AssertStep(string line)
    {
        using var json = JsonDocument.Parse(line);
        var root = json.RootElement;
        Assert.Subset(Set("trigger", "gates", "expect"), Names(root));

        var trigger = root.GetProperty("trigger");
        Assert.Subset(Set("kind", "outcome", "outputs", "park", "variant", "amount", "reason", "tokens"), Names(trigger));
        Assert.Equal(JsonValueKind.String, trigger.GetProperty("kind").ValueKind);
        if (trigger.TryGetProperty("park", out _))
        {
            Assert.Equal("child.parked", trigger.GetProperty("kind").GetString());
        }
        if (root.TryGetProperty("gates", out var gates))
        {
            Assert.All(gates.EnumerateObject(), g => Assert.Contains(g.Value.GetString(), _verdicts, StringComparer.Ordinal));
        }

        var expect = root.GetProperty("expect");
        Assert.Subset(Set("state", "park", "outcome"), Names(expect));
        Assert.Equal(JsonValueKind.String, expect.GetProperty("state").ValueKind);
    }

    private static HashSet<string> Set(params string[] names) => new(names, StringComparer.Ordinal);

    private static HashSet<string> Names(JsonElement element) => new(element.EnumerateObject().Select(p => p.Name), StringComparer.Ordinal);
}
