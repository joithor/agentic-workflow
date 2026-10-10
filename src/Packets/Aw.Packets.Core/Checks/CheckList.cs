namespace Aw.Packets.Core.Checks;

/// <summary>Collects one rule's outcomes: its failures, or a single pass when it found none.</summary>
internal sealed class CheckList
{
    private readonly List<CheckResult> _results = [];

    internal IReadOnlyList<CheckResult> Results => _results;

    internal void Rule(string rule, string passMessage, IEnumerable<(string Subject, string Message, string Fix)> failures)
    {
        var found = failures
            .Select(f => new CheckResult(rule, CheckState.Fail, f.Subject, f.Message, f.Fix))
            .OrderBy(static r => r.Subject, StringComparer.Ordinal)
            .ThenBy(static r => r.Message, StringComparer.Ordinal)
            .ToList();
        _results.AddRange(found.Count > 0 ? found : [new CheckResult(rule, CheckState.Pass, "packet", passMessage, Fix: null)]);
    }

    internal void Unchecked(string rule, string reason) =>
        _results.Add(new CheckResult(rule, CheckState.Unchecked, "packet", reason, Fix: null));
}
