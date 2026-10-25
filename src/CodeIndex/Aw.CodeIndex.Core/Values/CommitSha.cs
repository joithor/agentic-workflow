using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.CodeIndex.Core.Values;

/// <summary>A full git commit id: 40 lower-case hex digits.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CommitSha>))]
public readonly partial struct CommitSha : ITypedId<CommitSha, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("CommitSha must be 40 lower-case hex digits");

    [GeneratedRegex("^[0-9a-f]{40}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
