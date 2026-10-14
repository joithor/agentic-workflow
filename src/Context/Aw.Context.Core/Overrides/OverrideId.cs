using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Overrides;

/// <summary>An override's id, 'ovr_' and 16 hex digits of its content hash, so importing the same override twice keeps one row.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<OverrideId>))]
public readonly partial struct OverrideId : ITypedId<OverrideId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An override id is 'ovr_' and 16 lower-case hex digits");

    [GeneratedRegex("^ovr_[0-9a-f]{16}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
