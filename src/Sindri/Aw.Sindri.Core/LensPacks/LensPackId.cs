using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>A lens pack's id, as its pack.json and the profile name it: a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters (the profile's pack-id rule).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LensPackId>))]
public readonly partial struct LensPackId : ITypedId<LensPackId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A lens pack id is a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters");

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
