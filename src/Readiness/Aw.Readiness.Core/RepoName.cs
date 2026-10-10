using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Readiness.Core;

/// <summary>The name a repo's readiness history is kept under (readiness.yaml 'repo'), such as 'agentic-workflow'.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<RepoName>))]
public readonly partial struct RepoName : ITypedId<RepoName, string>
{
    private static Validation Validate(string value) => IsWellFormed(value)
        ? Validation.Ok
        : Validation.Invalid("RepoName must be 1-64 of a-z, 0-9, '.', '_' or '-', starting with a letter or digit");

    private static bool IsWellFormed(string value) =>
        value.Length is >= 1 and <= 64
        && IsLowerOrDigit(value[0])
        && value.All(c => IsLowerOrDigit(c) || c is '.' or '_' or '-');

    private static bool IsLowerOrDigit(char c) => c is (>= 'a' and <= 'z') or (>= '0' and <= '9');
}
