namespace Aw.Context.Core.Lenses;

/// <summary>One direct child of the path asked for.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Path">Its path in the repo, with a trailing '/' for a directory.</param>
/// <param name="Type">Directory or file.</param>
/// <param name="Status">Worst of its own status or its children's.</param>
/// <param name="Files">Files under it (1 for a file).</param>
/// <param name="Features">A file: the features it implements or tests, suggestions included. A directory: the confirmed ones its files implement or test. At most 10.</param>
/// <param name="Intent">The distinct records and decisions on those features, or null when it has no features.</param>
public sealed record FileMapEntry(string Name, string Path, FileEntryType Type, FileMapStatus Status, int Files, IReadOnlyList<FileFeature> Features, FileIntent? Intent);
