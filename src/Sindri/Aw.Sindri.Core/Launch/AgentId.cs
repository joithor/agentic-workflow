using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>An agent catalog entry's id (launcher spec SL3): 'claude-code', 'codex', 'cursor' or an added entry; a lowercase letter, then lowercase letters, digits and '-', 2 to 32 characters.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<AgentId>))]
public readonly partial struct AgentId : ITypedId<AgentId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("An agent id is a lowercase letter, then lowercase letters, digits or '-', 2 to 32 characters");

    [GeneratedRegex("^[a-z][a-z0-9-]{1,31}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
