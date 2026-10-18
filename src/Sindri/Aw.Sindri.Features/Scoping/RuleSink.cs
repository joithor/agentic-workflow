namespace Aw.Sindri.Features.Scoping;

/// <summary>What one rule reports in one run: its findings, and the fact keys it needed but did not find.</summary>
/// <param name="rule">The rule id the findings carry.</param>
/// <param name="facts">The facts of the run.</param>
internal sealed class RuleSink(string rule, ScopeFacts facts)
{
    private readonly List<ScopeFinding> _findings = [];
    private readonly List<string> _missing = [];

    /// <summary>The findings so far, failures and warnings.</summary>
    internal IReadOnlyList<ScopeFinding> Findings => _findings;

    /// <summary>The needed keys the facts lack, or empty when the rule could run.</summary>
    internal IReadOnlyList<string> Missing => _missing;

    /// <summary>Whether every key is present; when not, the rule is not run and the missing keys are recorded.</summary>
    internal bool Need(params string[] keys)
    {
        _missing.AddRange(facts.Missing(keys));
        return _missing.Count == 0;
    }

    /// <summary>A failing finding.</summary>
    internal void Fail(string? item, string message, string fix) => _findings.Add(new ScopeFinding(rule, item, message, fix, ScopeSeverity.Fail));

    /// <summary>A warning: shown, never changes an outcome.</summary>
    internal void Warn(string? item, string message, string fix) => _findings.Add(new ScopeFinding(rule, item, message, fix, ScopeSeverity.Warn));
}
