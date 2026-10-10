namespace Aw.Architecture.Tests.TypedIds.GuardFixtures;

/// <summary>Guard fixture: the id is a parameter of a constructor the serializer doesn't pick (a parameterless one exists).</summary>
#pragma warning disable VOG038 // deliberately optional: this fixture is what the guard must catch
internal sealed class ParameterlessCtorRecord
{
    public ParameterlessCtorRecord()
    {
    }

    public ParameterlessCtorRecord(ItemId item)
    {
        Item = item;
    }

    public ItemId Item { get; init; }
}
