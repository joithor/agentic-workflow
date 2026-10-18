namespace Aw.Sindri.Infrastructure.Workflows;

/// <summary>What the 'ReleaseClaim' activity is given (engine P7 plan D9, IDEA-033): the ended instance and the item it may hold the claim of.</summary>
/// <param name="Address">The instance address: the claim is released only while this instance still owns it.</param>
/// <param name="Item">The item.</param>
/// <param name="Repo">The key of the profile's repos 'fact:item' names, where the marker ref lives.</param>
internal sealed record ReleaseRun(string Address, string Item, string Repo);
