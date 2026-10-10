using System.Text.Json.Serialization;
using Aw.Kernel.Ids;
using Aw.Kernel.Json;
using Vogen;

namespace Aw.Architecture.Tests.TypedIds;

// A copy of the example in tests/Aw.Kernel.Tests/TypedIds, as the guard tests' compliant case (that project can't be referenced).
/// <summary>A constrained int: a token budget.</summary>
[ValueObject<int>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(Int32IdJsonConverter<TokenBudget>))]
internal readonly partial struct TokenBudget : ITypedId<TokenBudget, int>
{
    private static Validation Validate(int value) =>
        value < 0 ? Validation.Invalid("TokenBudget must be >= 0") : Validation.Ok;
}
