namespace Aw.Sindri.Core.Scheduling;

/// <summary>A held map draft as the publish decision reads it (Features maps it from the context store's hold and the version's heads).</summary>
/// <param name="Version">The held draft's number.</param>
/// <param name="Since">When the wait began (kept across supersedes).</param>
/// <param name="Heads">Each mapped repo's head as the build read it, by repo name.</param>
public sealed record MapHoldState(int Version, DateTimeOffset Since, IReadOnlyDictionary<string, MapRepoHead> Heads);
