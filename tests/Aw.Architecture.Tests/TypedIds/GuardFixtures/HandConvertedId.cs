using System.Text.Json.Serialization;
using Vogen;

namespace Aw.Architecture.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: a hand-written converter in user code, but no ITypedId.</summary>
[ValueObject<int>(comparison: ComparisonGeneration.Omit)]
[JsonConverter(typeof(HandIdConverter))]
internal readonly partial struct HandConvertedId
{
    private static Validation Validate(int value) => value < 0 ? Validation.Invalid("negative") : Validation.Ok;
}
