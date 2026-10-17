using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Ids;

/// <summary>
/// A job's kind: the catalog action id of the command that started it ('RunService', 'SyncSources', 'BuildContextMap'),
/// so the app labels a job from the command catalog with no vocabulary of its own.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<JobKind>))]
public readonly partial struct JobKind : ITypedId<JobKind, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A job kind is a PascalCase command name");

    [GeneratedRegex("^[A-Z][A-Za-z]{2,40}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
