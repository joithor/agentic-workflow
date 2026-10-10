namespace Aw.Kernel.Testing.Tests;

/// <summary>A class aggregate without value equality, the case the contract's SameEntity hook exists for.</summary>
/// <param name="id">Its id.</param>
/// <param name="text">Its text.</param>
public sealed class Note(ItemId id, string text)
{
    /// <summary>Its id.</summary>
    public ItemId Id { get; } = id;

    /// <summary>Its text.</summary>
    public string Text { get; } = text;

    /// <summary>A new instance with the same values, as a SQL repository returns.</summary>
    /// <returns>The copy.</returns>
    public Note Copy() => new(Id, Text);
}
