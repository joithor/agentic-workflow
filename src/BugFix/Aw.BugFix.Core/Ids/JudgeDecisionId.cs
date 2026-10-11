using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.BugFix.Core.Ids;

/// <summary>JudgeDecisionId: the id judge gives a stored decision (1–128 visible ASCII characters).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<JudgeDecisionId>))]
public readonly partial struct JudgeDecisionId : ITypedId<JudgeDecisionId, string>
{
    private static Validation Validate(string value) =>
        Shape.IsMatch(value) ? Validation.Ok : Validation.Invalid("JudgeDecisionId must be 1–128 visible ASCII characters");

    [GeneratedRegex("^[!-~]{1,128}\\z", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex Shape { get; }
}
