using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Trust;

/// <summary>
/// The fingerprint of the rules a text was cleaned with: the sanitizer's version, the built-in redaction rules and the
/// profile's extra patterns. Every stored document records it, so a document cleaned under other rules is cleaned again
/// at the next refresh and never served before that.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ScrubRuleset>))]
public readonly partial struct ScrubRuleset : ITypedId<ScrubRuleset, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A scrub ruleset is 16 to 64 lowercase hex characters");

    [GeneratedRegex("^[0-9a-f]{16,64}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 64 ? string.Empty : input;
}
