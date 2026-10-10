using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Values;

/// <summary>
/// RepoSlug: a repository as 'owner/name' (letters, digits, '.', '_' and '-' on each side, neither side '.' or '..'),
/// so it is also safe as two directory names under packets/.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RepoSlug>))]
public readonly partial struct RepoSlug : ITypedId<RepoSlug, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) && !value.Split('/').Any(static side => string.Equals(side, ".", StringComparison.Ordinal) || string.Equals(side, "..", StringComparison.Ordinal))
            ? Validation.Ok
            : Validation.Invalid("RepoSlug must be 'owner/name', neither side '.' or '..'");

    [GeneratedRegex("^[A-Za-z0-9._-]+/[A-Za-z0-9._-]+$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
