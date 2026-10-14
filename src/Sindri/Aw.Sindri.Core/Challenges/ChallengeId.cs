using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Sindri.Core.Challenges;

/// <summary>A server-issued challenge's id (engine spec §4.2): 'CHL-' and 32 lower-case hex digits; one use, then gone.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<ChallengeId>))]
public readonly partial struct ChallengeId : ITypedId<ChallengeId, string>
{
    /// <summary>The id for <paramref name="value"/>, written as 'CHL-' and its 32 hex digits.</summary>
    /// <param name="value">A fresh id from the id generator.</param>
    /// <returns>The id.</returns>
    public static ChallengeId For(Guid value) => From("CHL-" + value.ToString("N"));

    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("A challenge id is 'CHL-' and 32 lower-case hex digits");

    [GeneratedRegex("^CHL-[0-9a-f]{32}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
