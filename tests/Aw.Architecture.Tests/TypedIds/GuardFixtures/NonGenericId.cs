using Vogen;

namespace Aw.Kernel.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: the non-generic attribute form, no converter and no ITypedId.</summary>
[ValueObject(typeof(Guid), comparison: ComparisonGeneration.Omit)]
internal readonly partial struct NonGenericId
{
    private static Validation Validate(Guid value) => value == Guid.Empty ? Validation.Invalid("empty") : Validation.Ok;
}
