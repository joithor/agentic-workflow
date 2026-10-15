using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Eval;

/// <summary>A probe's id as its file writes it: 1 to 100 characters with no control or format characters (<see cref="ProbeRunner.Check"/> asks for a slug).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ProbeId>))]
public readonly partial struct ProbeId : ITypedId<ProbeId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A probe id is 1 to 100 characters with no control or format characters");

    [GeneratedRegex("^[^\\p{C}]{1,100}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 120 ? string.Empty : input;
}
