using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.CodeIndex.Core.Values;

/// <summary>A registered repo's short name (spec §4.1): letters, digits, '_', '-' and '.', no leading dot, at most 64 characters. It goes into file names, so nothing else is accepted (SND-CODEIDX-001).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RepoName>))]
public readonly partial struct RepoName : ITypedId<RepoName, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("RepoName must be 1 to 64 characters of letters, digits, '_', '-' and '.', not starting with '.'");

    [GeneratedRegex("^[A-Za-z0-9_-][A-Za-z0-9._-]{0,63}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
