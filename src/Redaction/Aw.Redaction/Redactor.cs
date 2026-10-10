namespace Aw.Redaction;

/// <summary>The built-in pattern set (spec §4c "One pattern source").</summary>
public sealed class Redactor : IRedactor
{
    /// <summary>A redactor over the built-in rules with <see cref="DefaultMatchTimeout"/>.</summary>
    public static Redactor Default { get; } = new();

    /// <summary>The per-pattern match timeout <see cref="Default"/> uses.</summary>
    public static TimeSpan DefaultMatchTimeout { get; } = TimeSpan.FromSeconds(1);

    /// <summary>Creates a redactor over the built-in rules with its own per-pattern match timeout.</summary>
    /// <param name="matchTimeout">The timeout for each pattern; must be positive.</param>
    /// <returns>The redactor.</returns>
    public static Redactor Create(TimeSpan matchTimeout) => throw new NotSupportedException("red: Create");

    /// <summary>The rule ids, in priority order (an earlier rule names an overlapping hit).</summary>
    public IReadOnlyList<string> RuleIds => _ruleIds;

    private readonly string[] _ruleIds = [];

    private Redactor()
    {
    }

    /// <summary>A redactor over <paramref name="rules"/> (tests inject a slow rule to prove the timeout blocks).</summary>
    internal Redactor(IReadOnlyList<RedactionRule> rules, TimeSpan matchTimeout)
    {
        _ = rules;
        _ = matchTimeout;
    }

    /// <inheritdoc/>
    public ScrubResult Scrub(string text) => throw new NotSupportedException("red: Scrub");
}
