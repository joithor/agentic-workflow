using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Launch;

/// <summary>
/// A Sindri launch's id, as AW_SINDRI_LAUNCH carries it (collector spec CO12): 1 to 64 of letters, digits, '.', '_', ':' and '-', not
/// starting with '.'. The session collector reads it from a process; the launcher (API P10a) mints it and may tighten the pattern.
/// </summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LaunchId>))]
public readonly partial struct LaunchId : ITypedId<LaunchId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A launch id is 1 to 64 of letters, digits, '.', '_', ':' or '-', not starting with '.'");

    [GeneratedRegex("^(?!\\.)(?!.*\\.\\.)[A-Za-z0-9._:-]{1,64}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
