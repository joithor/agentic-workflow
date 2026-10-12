using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Sources;

/// <summary>A source's change-feed cursor: opaque to everything but its adapter.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<SourceCursor>))]
public readonly partial struct SourceCursor : ITypedId<SourceCursor, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A cursor is 1 to 1024 characters with no control or format characters");

    [GeneratedRegex("^[^\\p{C}]{1,1024}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 1024 ? string.Empty : input;
}
