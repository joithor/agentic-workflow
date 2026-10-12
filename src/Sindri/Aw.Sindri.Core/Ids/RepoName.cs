using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A repo's name, as a profile repo entry names it: what scopes a session caller (X-Sindri-Repo, API spec §3.1). Letters,
/// digits, '.', '_' and '-', starting with a letter or digit, at most 100 characters: no path separator, space or control character.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RepoName>))]
public readonly partial struct RepoName : ITypedId<RepoName, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A repo name is 1 to 100 letters, digits, '.', '_' or '-', starting with a letter or digit");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
