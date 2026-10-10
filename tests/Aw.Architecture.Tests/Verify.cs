using Aw.Architecture.Tests.Rules;
using Xunit.Sdk;

namespace Aw.Architecture.Tests;

/// <summary>Assertions that print every violation, one per line (rule id, what was found, the fix).</summary>
internal static class Verify
{
    /// <summary>Fails with every violation listed when there is any.</summary>
    /// <param name="violations">The rule's violations.</param>
    public static void None(IEnumerable<Violation> violations)
    {
        var list = violations.Select(v => v.ToString()).Order(StringComparer.Ordinal).ToList();
        if (list.Count > 0)
        {
            throw new XunitException($"{list.Count} architecture violation(s):{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
        }
    }

    /// <summary>Asserts exactly the expected rule ids fire, each with a message naming what it found.</summary>
    /// <param name="violations">The rule's violations.</param>
    /// <param name="ruleId">The expected rule id.</param>
    /// <param name="found">Text each violation's Found must contain, one per expected violation.</param>
    public static void Exactly(IEnumerable<Violation> violations, string ruleId, params string[] found)
    {
        var list = violations.ToList();
        Assert.All(list, v => Assert.Equal(ruleId, v.RuleId));
        Assert.All(list, v => Assert.False(string.IsNullOrWhiteSpace(v.Fix)));
        Assert.Equal(found.Length, list.Count);

        // Each expected text claims a distinct violation, so one violation can't satisfy two expectations.
        var unclaimed = new List<Violation>(list);
        foreach (var f in found.OrderByDescending(f => f.Length))
        {
            var match = unclaimed.FirstOrDefault(v => v.Found.Contains(f, StringComparison.Ordinal));
            if (match is null)
            {
                Assert.Fail($"no unclaimed {ruleId} violation contains \"{f}\"; got:{Environment.NewLine}{string.Join(Environment.NewLine, list)}");
            }
            else
            {
                _ = unclaimed.Remove(match);
            }
        }
    }
}
