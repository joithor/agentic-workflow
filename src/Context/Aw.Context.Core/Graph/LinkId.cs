using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Graph;

/// <summary>An edge's id for review (S-4): 'edge:' and 16 hex digits of a hash of its ends and type, so a rebuild that finds the same fact gives it the same id.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<LinkId>))]
public readonly partial struct LinkId : ITypedId<LinkId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A link id is 'edge:' and 16 lower-case hex digits");

    [GeneratedRegex("^edge:[0-9a-f]{16}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 40 ? string.Empty : input;
}
