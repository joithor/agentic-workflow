using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Sources;

/// <summary>The canonical key of one real-world record across adapters (spec D16), such as 'repo:split-panes/docs/architecture.md' or 'linear:issue/&lt;uuid&gt;'.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RecordKey>))]
public readonly partial struct RecordKey : ITypedId<RecordKey, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A record key is a lower-case scheme, a colon and 1 to 1024 characters with no control or format characters");

    [GeneratedRegex("^[a-z][a-z0-9-]*:[^\\p{C}]{1,1024}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 1100 ? string.Empty : input;
}
