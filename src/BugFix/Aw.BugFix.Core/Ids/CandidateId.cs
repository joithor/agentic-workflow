using System.Globalization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.BugFix.Core.Ids;

/// <summary>CandidateId: a fix candidate, numbered in recording order (c1, c2, …).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CandidateId>))]
public readonly partial struct CandidateId : ITypedId<CandidateId, string>
{
    /// <summary>The id of the candidate recorded as number <paramref name="number"/> (1-based).</summary>
    /// <param name="number">The candidate's position in recording order.</param>
    /// <returns>c&lt;number&gt;.</returns>
    public static CandidateId Numbered(int number) => From(string.Create(CultureInfo.InvariantCulture, $"c{number}"));

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("CandidateId must be c followed by a number, e.g. c1");

    [GeneratedRegex("^c[1-9][0-9]{0,5}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
