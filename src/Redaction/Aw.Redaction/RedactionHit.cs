namespace Aw.Redaction;

/// <summary>One redacted span of the input. It names the rule and the span, never the matched value.</summary>
/// <param name="Rule">The rule id, e.g. github-token; the placeholder is [REDACTED:Rule].</param>
/// <param name="Start">The first redacted UTF-16 index in the original text.</param>
/// <param name="End">The index after the last redacted UTF-16 code unit in the original text (exclusive).</param>
public readonly record struct RedactionHit(string Rule, int Start, int End);
