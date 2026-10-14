using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Sources;

/// <summary>A record's version in its source: a blob or commit sha, an updatedAt timestamp or a map version.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SourceVersion>))]
public readonly partial struct SourceVersion : ITypedId<SourceVersion, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A source version is 1 to 128 characters with no spaces, control or format characters");

    [GeneratedRegex("^[^\\p{C}\\s]{1,128}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 128 ? string.Empty : input;
}
