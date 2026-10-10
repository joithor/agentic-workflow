using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Kernel.Tests.TypedIds;

/// <summary>A constrained int: a token budget.</summary>
[ValueObject<int>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(Int32IdJsonConverter<TokenBudget>))]
internal readonly partial struct TokenBudget : ITypedId<TokenBudget, int>
{
    private static Validation Validate(int value) =>
        value < 0 ? Validation.Invalid("TokenBudget must be >= 0") : Validation.Ok;
}
