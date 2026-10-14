using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Engine;

/// <summary>A profile repo's name, as the engine uses it ('repo:' keys, a session's X-Sindri-Repo scope): 1 to 100 letters, digits, '.', '_' or '-'.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ContextRepoName>))]
public readonly partial struct ContextRepoName : ITypedId<ContextRepoName, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A repo name is 1 to 100 letters, digits, '.', '_' or '-', starting with a letter or digit");

    [GeneratedRegex("^[A-Za-z0-9][A-Za-z0-9._-]{0,99}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 100 ? string.Empty : input;
}
