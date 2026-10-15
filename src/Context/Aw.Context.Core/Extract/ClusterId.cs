using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Extract;

/// <summary>A cluster's stable name inside one build ('c3'): letters, digits, '-' and '_'.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ClusterId>))]
public readonly partial struct ClusterId : ITypedId<ClusterId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A cluster id is 1 to 32 letters, digits, '-' or '_', starting with a letter or digit");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9_-]{0,31}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
