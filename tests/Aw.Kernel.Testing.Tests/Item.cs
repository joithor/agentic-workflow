namespace Aw.Kernel.Testing.Tests;

/// <summary>A sample aggregate for the builder and contract examples.</summary>
/// <param name="Id">Its id.</param>
/// <param name="Title">Its title.</param>
/// <param name="Size">Its size.</param>
public sealed record Item(ItemId Id, string Title, Size Size);
