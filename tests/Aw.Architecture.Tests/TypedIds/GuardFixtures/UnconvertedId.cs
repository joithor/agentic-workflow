using Aw.Kernel.Ids;
using Vogen;

namespace Aw.Kernel.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: an id with no [JsonConverter] (it would write {}).</summary>
[ValueObject<string>(comparison: ComparisonGeneration.Omit)]
internal readonly partial struct UnconvertedId : ITypedId<UnconvertedId, string>
{
    private static Validation Validate(string value) => string.IsNullOrEmpty(value) ? Validation.Invalid("empty") : Validation.Ok;
}
