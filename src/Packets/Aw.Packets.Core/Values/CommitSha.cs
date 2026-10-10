using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Packets.Core.Values;

/// <summary>CommitSha: a full git commit sha, 40 (SHA-1) or 64 (SHA-256) lower-case hex digits.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CommitSha>))]
public readonly partial struct CommitSha : ITypedId<CommitSha, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("CommitSha must be 40 or 64 lower-case hex digits");

    [GeneratedRegex("^(?:[0-9a-f]{40}|[0-9a-f]{64})\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
