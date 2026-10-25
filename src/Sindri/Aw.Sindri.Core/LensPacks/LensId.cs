using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.LensPacks;

/// <summary>A lens's id in its pack: a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters. The lens log and the lens queue name a lens by it.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LensId>))]
public readonly partial struct LensId : ITypedId<LensId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A lens id is a lowercase letter, then lowercase letters, digits or '-', 2 to 40 characters");

    [GeneratedRegex("^[a-z][a-z0-9-]{1,39}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
