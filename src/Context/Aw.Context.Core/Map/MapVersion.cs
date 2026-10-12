namespace Aw.Context.Core.Map;

/// <summary>One map version (table map_version).</summary>
/// <param name="Number">Its number (shown as "map v13"), assigned by the store in order.</param>
/// <param name="Repo">The repo it maps.</param>
/// <param name="Commit">The commit it was built at, or null for an imported map that names none.</param>
/// <param name="KeyVersion">The EntryKey version of its keys.</param>
/// <param name="Created">When it was written.</param>
/// <param name="Status">Its lifecycle state.</param>
public sealed record MapVersion(int Number, string Repo, string? Commit, int KeyVersion, DateTimeOffset Created, MapVersionStatus Status);
