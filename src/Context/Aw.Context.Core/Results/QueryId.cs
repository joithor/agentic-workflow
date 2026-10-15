using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Context.Core.Results;

/// <summary>A query's id ('q_' and a compact id), which traces it in the query log.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<QueryId>))]
public readonly partial struct QueryId : ITypedId<QueryId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A query id is 'q_' followed by 1 to 64 letters or digits");

    [GeneratedRegex("^q_[A-Za-z0-9]{1,64}\\z", RegexOptions.CultureInvariant | RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }

    private static string NormalizeInput(string input) => input.Length > 66 ? string.Empty : input;
}
