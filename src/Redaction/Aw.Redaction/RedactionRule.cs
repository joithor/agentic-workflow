using System.Text.RegularExpressions;

namespace Aw.Redaction;

/// <summary>One pattern. When the pattern has a group named v, only that group is redacted (the value of a
/// KEY=value pair, the token after Bearer); otherwise the whole match is.</summary>
/// <param name="Id">The rule id the placeholder names.</param>
/// <param name="Pattern">The regex: explicit ASCII classes, no \b, \s or \d (spec §4c).</param>
/// <param name="Options">Extra options; <see cref="RegexOptions.CultureInvariant"/> and
/// <see cref="RegexOptions.ExplicitCapture"/> are always added.</param>
internal sealed record RedactionRule(string Id, string Pattern, RegexOptions Options = RegexOptions.None);
