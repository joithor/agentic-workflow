using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Readiness.Core;

/// <summary>A readiness criterion's stable id, such as 'SV-1': a pillar code, a dash and a number.</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(StringIdJsonConverter<CriterionId>))]
public readonly partial struct CriterionId : ITypedId<CriterionId, string>
{
    private static Validation Validate(string value) => IsWellFormed(value)
        ? Validation.Ok
        : Validation.Invalid("CriterionId must be 2-4 capital letters, a dash and 1-2 digits, such as SV-1");

    private static bool IsWellFormed(string value)
    {
        var dash = value.IndexOf('-', StringComparison.Ordinal);
        return dash is >= 2 and <= 4
            && value.Length - dash - 1 is >= 1 and <= 2
            && value[..dash].All(c => c is >= 'A' and <= 'Z')
            && value[(dash + 1)..].All(c => c is >= '0' and <= '9');
    }
}
