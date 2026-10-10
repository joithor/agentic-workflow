namespace Aw.Kernel.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: a body-style record whose id is an optional init property.</summary>
#pragma warning disable VOG038 // deliberately optional: this fixture is what the guard must catch
internal sealed class OptionalIdRecord
{
    public ItemId Item { get; init; }
}
